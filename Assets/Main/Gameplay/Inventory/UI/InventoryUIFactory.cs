using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包 UI 的运行时构建工具 —— 统一"占位美术"和布局口径。
///
/// 为什么用代码构建（而不是预制体）：
///   - 当前没有任何 UI 素材，代码构建可以保证零素材可运行；
///   - 后续接入美术时，只需替换本类里的 sprite/颜色，或把 InventoryUI 换成读取预制体；
///   - 圆角九宫格 Sprite 由代码生成，不依赖任何图片资源。
///
/// 注意：这里用的是 uGUI 原生 Image/Text/Button，字体由调用方传入（推荐项目里的中文字体），
/// 这样中文在任何环境下都能正常显示，不会出现 TMP 字体缺字。
/// </summary>
public static class InventoryUIFactory
{
    private static Sprite _roundedSprite;
    private static Sprite _whiteSprite;
    private static Font _defaultFont;

    /// <summary>程序生成的圆角九宫格（用于面板/槽位/按钮底色）</summary>
    public static Sprite RoundedSprite
    {
        get
        {
            if (_roundedSprite == null) _roundedSprite = CreateRoundedSprite(64, 16);
            return _roundedSprite;
        }
    }

    /// <summary>纯白 Sprite（给图标占位/纯色块用）</summary>
    public static Sprite WhiteSprite
    {
        get
        {
            if (_whiteSprite == null) _whiteSprite = CreateWhiteSprite(8);
            return _whiteSprite;
        }
    }

    /// <summary>未指定字体时的兜底（Unity 2022 内置 LegacyRuntime.ttf）</summary>
    public static Font DefaultFont
    {
        get
        {
            if (_defaultFont == null)
            {
                try { _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                catch { _defaultFont = null; }

                if (_defaultFont == null)
                {
                    try { _defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                    catch { _defaultFont = null; }
                }
            }
            return _defaultFont;
        }
    }

    // ==================== 创建节点 ====================

    public static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    /// <summary>创建纯色/圆角图片（默认不接收射线，需要点击的由调用方打开 raycastTarget）</summary>
    public static Image CreateImage(string name, Transform parent, Color color, Sprite sprite = null, bool sliced = false)
    {
        RectTransform rt = CreateRect(name, parent);
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite != null ? sprite : RoundedSprite;
        image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static Text CreateText(string name, Transform parent, Font font, string content,
                                  int fontSize, Color color, TextAnchor alignment)
    {
        RectTransform rt = CreateRect(name, parent);
        var text = rt.gameObject.AddComponent<Text>();
        text.font = font != null ? font : DefaultFont;
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    public static Button CreateButton(Transform parent, string name, Font font, string label,
                                      Color background, Color textColor, int fontSize, out Text labelText)
    {
        Image image = CreateImage(name, parent, background, RoundedSprite, true);
        image.raycastTarget = true;

        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
        colors.pressedColor = new Color(0.72f, 0.74f, 0.78f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        labelText = CreateText("Label", image.transform, font, label, fontSize, textColor, TextAnchor.MiddleCenter);
        Stretch(labelText.rectTransform, 6f, 6f, 2f, 2f);
        return button;
    }

    // ==================== 布局辅助 ====================

    /// <summary>铺满父节点（带四边内边距）</summary>
    public static void Stretch(RectTransform rt, float left, float right, float bottom, float top)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    /// <summary>以父节点左上角为原点定位（y 为向下偏移）</summary>
    public static void PlaceTopLeft(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(width, height);
    }

    /// <summary>以父节点右上角为原点定位（x/y 为距右/上边缘的距离）</summary>
    public static void PlaceTopRight(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-x, -y);
        rt.sizeDelta = new Vector2(width, height);
    }

    /// <summary>以父节点左下角为原点定位</summary>
    public static void PlaceBottomLeft(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(width, height);
    }

    // ==================== 程序化 Sprite ====================

    private static Sprite CreateRoundedSprite(int size, int radius)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "UI_Rounded(Generated)",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                float cx = Mathf.Clamp(px, radius, size - radius);
                float cy = Mathf.Clamp(py, radius, size - radius);
                float distance = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
                float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        var border = new Vector4(radius + 2, radius + 2, radius + 2, radius + 2);
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                             100f, 0, SpriteMeshType.FullRect, border);
    }

    private static Sprite CreateWhiteSprite(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "UI_White(Generated)",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(1f, 1f, 1f, 1f);

        texture.SetPixels32(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                             100f, 0, SpriteMeshType.FullRect, new Vector4(2, 2, 2, 2));
    }
}
