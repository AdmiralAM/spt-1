using System.Threading;
using UnityEngine;

namespace SPTItemIntelligence
{
    public sealed class ItemHoverOverlaySink : IItemHoverViewSink
    {
        ItemHoverText current = ItemHoverText.Empty;
        bool drawingDisabled;

        public ItemHoverText Current => Volatile.Read(ref current);

        public void Show(ItemHoverText text)
        {
            Interlocked.Exchange(ref current, text ?? ItemHoverText.Empty);
        }

        public void Clear()
        {
            Interlocked.Exchange(ref current, ItemHoverText.Empty);
        }

        public void Draw()
        {
            if (drawingDisabled) return;
            ItemHoverText text = Current;
            if (text == null || !text.HasData) return;

            try
            {
                Vector3 mouse = Input.mousePosition;
                float width = 300f;
                float height = 66f;
                float x = Mathf.Clamp(mouse.x + 18f, 0f, Mathf.Max(0f, Screen.width - width));
                float y = Mathf.Clamp(Screen.height - mouse.y + 18f, 0f, Mathf.Max(0f, Screen.height - height));
                Rect panel = new Rect(x, y, width, height);
                GUI.Box(panel, GUIContent.none);
                GUI.Label(new Rect(x + 10f, y + 6f, width - 20f, 18f), text.Primary);
                GUI.Label(new Rect(x + 10f, y + 24f, width - 20f, 18f), text.Secondary);
                GUI.Label(new Rect(x + 10f, y + 42f, width - 20f, 18f), text.Status);
            }
            catch
            {
                drawingDisabled = true;
                Clear();
            }
        }
    }
}
