using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一次性工具：把 Hierarchy 里搭好的背包 UI 自动接到 InventoryUI / PickupPromptUI 上，
/// 并顺手修正槽位预制体的两个小问题（数量文字颜色、根节点圆角图）。
/// 接完线后本文件可以直接删除，不影响运行时逻辑。
/// </summary>
public static class InventoryUIAutoWire
{
    private const string SlotPrefabDir = "Assets/Main/Gameplay/Inventory/Prefabs/";
    private const string SlotPrefabName = "Solt";   // 实际文件名是 Solt.prefab
    private const string SlotPrefabNameAlt = "Slot";

    [MenuItem("Tools/RPG/自动接线背包 UI")]
    public static void Wire()
    {
        InventoryUI ui = Object.FindObjectOfType<InventoryUI>(true);
        Transform canvas = FindTransform("InventoryCanvas");
        if (ui == null || canvas == null)
        {
            Debug.LogError("[自动接线] 找不到 InventoryUI 或 InventoryCanvas，请先确认 Hierarchy。");
            return;
        }

        var so = new SerializedObject(ui);
        Assign(so, "uiRoot", canvas.gameObject);
        Assign(so, "gridContent", FindIn<RectTransform>(canvas, "Content"));
        Assign(so, "capacityText", FindIn<TMP_Text>(canvas, "Capacity"));
        Assign(so, "detailIcon", FindIn<Image>(canvas, "DetailIcon"));
        Assign(so, "detailName", FindIn<TMP_Text>(canvas, "DetailName"));
        Assign(so, "detailType", FindIn<TMP_Text>(canvas, "DetailType"));
        Assign(so, "detailDesc", FindIn<TMP_Text>(canvas, "DetailDesc"));

        Transform ghost = FindIn(canvas, "DragGhost");
        Assign(so, "dragGhost", ghost as RectTransform);
        Assign(so, "dragGhostIcon", ghost != null ? ghost.GetComponent<Image>() : null);
        Assign(so, "dragGhostCount", ghost != null ? FindIn<TMP_Text>(ghost, "Count") : null);

        Assign(so, "closeButton", FindIn<Button>(canvas, "CloseButton"));
        Assign(so, "sortByTypeButton", FindIn<Button>(canvas, "SortByType"));
        Assign(so, "sortByCountButton", FindIn<Button>(canvas, "SortByCount"));

        string slotPath = ResolveSlotPrefabPath();
        InventorySlotView slotPrefab = slotPath != null
            ? AssetDatabase.LoadAssetAtPath<InventorySlotView>(slotPath)
            : null;
        Assign(so, "slotPrefab", slotPrefab);

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ui);

        // 拾取提示：先把 PickupCanvas 搭好才会自动接上
        PickupPromptUI prompt = Object.FindObjectOfType<PickupPromptUI>(true);
        Transform pickupCanvas = FindTransform("PickupCanvas");
        if (prompt != null && pickupCanvas != null)
        {
            Transform panel = FindIn(pickupCanvas, "PromptPanel");
            var pso = new SerializedObject(prompt);
            Assign(pso, "panelRoot", panel != null ? panel.gameObject : null);
            Assign(pso, "label", FindIn<TMP_Text>(pickupCanvas, "Label"));
            pso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prompt);
        }
        else
        {
            Debug.Log("[自动接线] 还没搭 PickupCanvas，拾取提示暂时跳过（以后搭好再点一次这个菜单即可）。");
        }

        if (slotPath != null) FixSlotPrefab(slotPath);

        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("[自动接线] 完成，场景已保存。按 Tab 试试背包。");
    }

    /// <summary>把字段接到引用上；找不到只警告，不中断</summary>
    private static void Assign(SerializedObject so, string field, Object value)
    {
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
        {
            Debug.LogWarning($"[自动接线] 字段 {field} 不存在，可能脚本已改动。");
            return;
        }
        if (value == null)
        {
            Debug.LogWarning($"[自动接线] Hierarchy 里没找到 {field} 对应的物体。");
            return;
        }
        property.objectReferenceValue = value;
    }

    private static string ResolveSlotPrefabPath()
    {
        string path = SlotPrefabDir + SlotPrefabName + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return path;

        path = SlotPrefabDir + SlotPrefabNameAlt + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return path;

        Debug.LogWarning("[自动接线] 在 Prefabs 目录下找不到槽位预制体。");
        return null;
    }

    /// <summary>槽位预制体的收尾修正：圆角边框、数量文字白色且不挡鼠标</summary>
    private static void FixSlotPrefab(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null) return;

        Image border = root.GetComponent<Image>();
        if (border != null)
        {
            border.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            border.type = Image.Type.Sliced;
            border.raycastTarget = true;
        }

        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            text.text = string.Empty;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.Refresh();
    }

    private static Transform FindTransform(string name)
    {
        foreach (Transform t in Object.FindObjectsOfType<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    private static Transform FindIn(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    private static T FindIn<T>(Transform root, string name) where T : Component
    {
        Transform t = FindIn(root, name);
        if (t == null) return null;

        T component = t.GetComponent<T>();
        if (component == null)
            Debug.LogWarning($"[自动接线] {name} 上没有 {typeof(T).Name} 组件。");
        return component;
    }
}
