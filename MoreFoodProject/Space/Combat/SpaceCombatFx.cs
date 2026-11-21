using UnityEngine;

namespace MoreFood_KFC
{
    // 战斗特效底层资源（CombatFx 的光团贴图来源）

    internal static class SpaceCombatFx
    {
        private static Sprite ball;

        internal static Sprite Ball()
        {
            if (ball != null)
            {
                return ball;
            }
            int n = 32;
            Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            float r = n * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = x - r + 0.5f;
                    float dy = y - r + 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            ball = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f));
            ball.name = "SpaceFxBall";
            return ball;
        }
    }
}
