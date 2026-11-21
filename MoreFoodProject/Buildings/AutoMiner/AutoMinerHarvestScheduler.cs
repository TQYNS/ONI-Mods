// Buildings/AutoMiner/AutoMinerHarvestScheduler.cs — 采集调度器：抢占建锁、每机 FIFO 队列、工时推进、空闲超时

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    internal sealed class HarvestLease
    {
        internal Harvestable target;
        internal AutoMiner miner;
        internal int cell;
        internal float stamp;
    }

    // 每机 FIFO 采集队列：Pending 先来后到、Active 当前目标；IdleStart<0 活跃，>=0 空闲超时计时中
    internal sealed class HarvestQueue
    {
        internal readonly List<HarvestLease> Pending = new List<HarvestLease>();
        internal HarvestLease Active;
        internal float Progress;
        internal float IdleStart = -1f;
    }

    internal static class AutoMinerHarvestScheduler
    {
        private const string TAG = "[MoreFood/AutoMiner]";
        private const float MinScanInterval = 1f;
        private const float FallbackInterval = 600f;
        private const float LeaseTimeout = 30f;

        // 单株采集工时 5 秒，对齐 20 级园艺复制人（推导见生态.md §6.5）
        internal const float HarvestSeconds = 5f;

        private static readonly object Gate = new object();
        private static readonly HashSet<AutoMiner> Miners = new HashSet<AutoMiner>();
        private static readonly HashSet<Harvestable> Pending = new HashSet<Harvestable>();
        private static readonly Dictionary<Harvestable, HarvestLease> ByTarget = new Dictionary<Harvestable, HarvestLease>();
        private static readonly Dictionary<int, HarvestLease> ByCell = new Dictionary<int, HarvestLease>();
        private static readonly Dictionary<AutoMiner, HarvestQueue> Queues = new Dictionary<AutoMiner, HarvestQueue>();

        private static float lastScan;
        private static float lastAnyScan;
        private static bool scanning;

        private static HarvestQueue QueueFor(AutoMiner miner, bool create = true)
        {
            HarvestQueue q;
            if (Queues.TryGetValue(miner, out q)) return q;
            if (!create) return null;
            q = new HarvestQueue();
            Queues[miner] = q;
            return q;
        }

        internal static void RegisterMiner(AutoMiner miner)
        {
            if (miner == null) return;
            lock (Gate) Miners.Add(miner);
        }

        internal static void UnregisterMiner(AutoMiner miner)
        {
            if (miner == null) return;
            lock (Gate)
            {
                Miners.Remove(miner);
                ReleaseAll(QueueFor(miner, false), true);
                Queues.Remove(miner);
            }
        }

        internal static void Signal(Harvestable plant)
        {
            if (plant == null) return;
            lock (Gate)
            {
                if (Miners.Count == 0) return;
                Pending.Add(plant);
            }
            Flush(false);
        }

        // 成熟 Prefix 抢在原生建 chore 前锁定；守卫拒绝建 chore 实现预防性独占（详见生态.md §6.2）
        internal static void ClaimOnReady(Harvestable plant)
        {
            if (plant == null) return;
            try { TryAssign(plant, true); }
            catch (Exception e) { MFLog.HotFail("autominer.claim", e); }
        }

        // 切到采集模式 / 采矿机出生时的一次性全范围扫描：把范围内所有可收获、无主的植物一次性入队锁定。
        internal static void ScanNow(AutoMiner miner)
        {
            if (miner == null) return;
            try
            {
                var mode = miner.GetComponent<AutoMinerHarvestMode>();
                if (mode == null || !mode.harvestMode) return;
                var items = Components.Harvestables.Items;
                for (int i = 0; i < items.Count; i++)
                {
                    var h = items[i];
                    if (h == null || !h.CanBeHarvested) continue;
                    if (h.HasChore() || h.worker != null) continue;
                    int cell = h.GetCell();
                    if (!Grid.IsValidCell(cell) || !InRange(miner, cell)) continue;
                    TryAssign(h, false, miner);
                }
            }
            catch (Exception e) { MFLog.HotFail("autominer.scannow", e); }
        }

        internal static void Tick()
        {
            Flush(false);
            if (Time.time - lastAnyScan >= FallbackInterval) Flush(true);
        }

        private static void Flush(bool full)
        {
            if (scanning) return;
            if (!full)
            {
                if (Time.time - lastScan < MinScanInterval) return;
                lock (Gate) { if (Pending.Count == 0) return; }
            }
            scanning = true;
            lastScan = Time.time;
            lastAnyScan = Time.time;
            try
            {
                List<Harvestable> batch = new List<Harvestable>();
                lock (Gate)
                {
                    if (full)
                    {
                        var items = Components.Harvestables.Items;
                        for (int i = 0; i < items.Count; i++)
                        {
                            var h = items[i];
                            if (h != null && h.CanBeHarvested) batch.Add(h);
                        }
                    }
                    else
                    {
                        batch.AddRange(Pending);
                        Pending.Clear();
                    }
                }
                for (int i = 0; i < batch.Count; i++) TryAssign(batch[i]);
            }
            catch (Exception e) { MFLog.HotFail("autominer.flush", e); }
            finally { scanning = false; }
        }

        // 锁定植物并入最近采矿机 FIFO 队列；assumeReady 供成熟 Prefix、only 限定即时扫描的分配
        private static void TryAssign(Harvestable plant, bool assumeReady = false, AutoMiner only = null)
        {
            if (plant == null) return;
            if (!assumeReady && !plant.CanBeHarvested) return;
            lock (Gate)
            {
                if (ByTarget.ContainsKey(plant)) return;          // 同类建筑已锁 → 不争抢
                if (plant.HasChore() || plant.worker != null) return; // 复制人已认领 → 不争抢
            }
            int cell = plant.GetCell();
            if (!Grid.IsValidCell(cell)) return;
            AutoMiner best = null;
            float bestDist = float.MaxValue;
            lock (Gate)
            {
                foreach (var m in Miners)
                {
                    if (m == null) continue;
                    if (only != null && m != only) continue;
                    var mode = m.GetComponent<AutoMinerHarvestMode>();
                    if (mode == null || !mode.harvestMode) continue;
                    if (!InRange(m, cell)) continue;
                    int judge = JudgeCell(m);
                    int jx, jy, cx, cy;
                    Grid.CellToXY(judge, out jx, out jy);
                    Grid.CellToXY(cell, out cx, out cy);
                    float dx = jx - cx, dy = jy - cy;
                    float d = dx * dx + dy * dy;
                    if (d < bestDist) { bestDist = d; best = m; }
                }
                if (best == null) return;
                var lease = new HarvestLease { target = plant, miner = best, cell = cell, stamp = Time.time };
                ByTarget[plant] = lease;
                ByCell[cell] = lease;
                QueueFor(best).Pending.Add(lease);   // 先来后到入队
            }
        }

        // 推进队列：校验/取出当前目标，返回 null 表示无有效目标（进入空闲计时）。
        internal static HarvestLease Advance(AutoMiner miner)
        {
            lock (Gate)
            {
                HarvestQueue q = QueueFor(miner, false);
                if (q == null) return null;
                if (q.Active != null && !LeaseValid(q.Active))
                {
                    var dead = q.Active;
                    q.Active = null;
                    q.Progress = 0f;
                    Release(dead, false);
                }
                if (q.Active != null)
                {
                    q.IdleStart = -1f;
                    return q.Active;
                }
                while (q.Pending.Count > 0)
                {
                    var next = q.Pending[0];
                    q.Pending.RemoveAt(0);
                    if (!LeaseValid(next)) { Release(next, false); continue; }
                    q.Active = next;
                    q.Progress = 0f;
                    q.IdleStart = -1f;
                    return next;
                }
                if (q.IdleStart < 0f) q.IdleStart = Time.time;
                return null;
            }
        }

        private static bool LeaseValid(HarvestLease lease)
        {
            var h = lease.target;
            return h != null && h.CanBeHarvested && !h.HasChore() && h.worker == null;
        }

        internal static HarvestLease CurrentLease(AutoMiner miner)
        {
            lock (Gate)
            {
                HarvestQueue q = QueueFor(miner, false);
                return q == null || q.Active == null ? null : q.Active;
            }
        }

        // 采集工时推进：返回 true 表示达到 HarvestSeconds、可以收割。
        internal static bool TickProgress(AutoMiner miner, float dt)
        {
            lock (Gate)
            {
                HarvestQueue q = QueueFor(miner, false);
                if (q == null || q.Active == null) return false;
                q.IdleStart = -1f;                 // 正在采集 = 活跃，重置空闲超时
                q.Progress += dt;                  // dt 为 SIM_200ms 游戏秒，随游戏加速
                return q.Progress >= HarvestSeconds;
            }
        }

        internal static void MarkIdle(AutoMiner miner)
        {
            lock (Gate)
            {
                HarvestQueue q = QueueFor(miner, false);
                if (q != null && q.IdleStart < 0f) q.IdleStart = Time.time;
            }
        }

        // 采集停止后开始计时；超时则释放该机全部锁并归还 chore。中途恢复采集会由 Advance/TickProgress 重算。
        internal static void DropStale(AutoMiner miner)
        {
            lock (Gate)
            {
                HarvestQueue q = QueueFor(miner, false);
                if (q == null || q.IdleStart < 0f) return;
                if (Time.time - q.IdleStart <= LeaseTimeout) return;
                ReleaseAll(q, true);
            }
        }

        internal static bool IsLocked(Harvestable plant)
        {
            lock (Gate) return plant != null && ByTarget.ContainsKey(plant);
        }

        // 收割完成 / 目标失效：释放当前 Active，不动队列其余目标。
        internal static void Complete(AutoMiner miner)
        {
            lock (Gate)
            {
                HarvestQueue q = QueueFor(miner, false);
                if (q == null) return;
                if (q.Active != null) Release(q.Active, false);
                q.Active = null;
                q.Progress = 0f;
            }
        }

        // 模式切回挖矿：释放该机全部锁（含队列），归还 chore 给复制人。
        internal static void ReleaseAllFor(AutoMiner miner)
        {
            if (miner == null) return;
            lock (Gate)
            {
                ReleaseAll(QueueFor(miner, false), true);
            }
        }

        private static void ReleaseAll(HarvestQueue q, bool restoreChore)
        {
            if (q == null) return;
            if (q.Active != null) { Release(q.Active, restoreChore); q.Active = null; }
            for (int i = 0; i < q.Pending.Count; i++) Release(q.Pending[i], restoreChore);
            q.Pending.Clear();
            q.Progress = 0f;
            q.IdleStart = -1f;
        }

        private static void Release(HarvestLease lease, bool restoreChore)
        {
            if (lease == null) return;
            if (lease.target != null) ByTarget.Remove(lease.target);
            HarvestLease byCell;
            if (ByCell.TryGetValue(lease.cell, out byCell) && byCell == lease) ByCell.Remove(lease.cell);

            // 异常释放归还 chore 防植物悬空；正常收割完成 restoreChore=false 避免重复建
            if (restoreChore && lease.target != null)
            {
                try
                {
                    var h = lease.target;
                    if (h.CanBeHarvested && !h.HasChore() && h.worker == null)
                    {
                        var d = h.harvestDesignatable;
                        if (d == null || d.HarvestWhenReady) h.OnMarkedForHarvest();
                    }
                }
                catch (Exception e) { MFLog.HotFail("autominer.restore", e); }
            }
        }

        private static bool InRange(AutoMiner miner, int cell)
        {
            int origin = Grid.PosToCell(miner.gameObject);
            if (!Grid.IsValidCell(origin)) return false;
            int ox, oy, cx, cy;
            Grid.CellToXY(origin, out ox, out oy);
            Grid.CellToXY(cell, out cx, out cy);
            int wx = cx - ox, wy = cy - oy;
            var rot = miner.GetComponent<Rotatable>();
            Orientation o = rot != null ? rot.Orientation : Orientation.Neutral;
            int lx, ly;
            switch (o)
            {
                case Orientation.R90: lx = -wy; ly = wx; break;
                case Orientation.R180: lx = -wx; ly = -wy; break;
                case Orientation.R270: lx = wy; ly = -wx; break;
                case Orientation.FlipH: lx = -wx; ly = wy; break;
                case Orientation.FlipV: lx = wx; ly = -wy; break;
                default: lx = wx; ly = wy; break;
            }
            return lx >= miner.x && lx < miner.x + miner.width && ly >= miner.y && ly < miner.y + miner.height;
        }

        private static int JudgeCell(AutoMiner miner)
        {
            int origin = Grid.PosToCell(miner.gameObject);
            CellOffset off = miner.vision_offset;
            var rot = miner.GetComponent<Rotatable>();
            if (rot != null) off = rot.GetRotatedCellOffset(off);
            return Grid.OffsetCell(origin, off);
        }

        internal static void ClearAll()
        {
            lock (Gate)
            {
                foreach (var kv in Queues) ReleaseAll(kv.Value, false);
                Queues.Clear();
                Pending.Clear();
            }
        }
    }
}
