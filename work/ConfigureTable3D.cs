using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>3D 테이블 전투의 공용 HUD 프리팹과 장면 참조를 구성한다.</summary>
public static class ConfigureTable3D
{
    /// <summary>기존 프리팹의 표시 위치를 조정하고 전용 재질 및 테이블 컴포넌트를 장면에 연결한다.</summary>
    public static string Run()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Stop Play Mode before configuring scene assets.");
        string folder = "Assets/Materials";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "Materials");
        Material piece = MaterialAsset(folder + "/SpellTablePiece.mat", "Universal Render Pipeline/Lit");
        piece.SetFloat("_Smoothness", 0.3f); piece.SetFloat("_Metallic", 0.12f);
        Material line = MaterialAsset(folder + "/SpellTableLine.mat", "Universal Render Pipeline/Unlit");
        GameObject root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/SpellCombatScreen.prefab");
        try
        {
            SpellCombatScreen screen = root.GetComponent<SpellCombatScreen>();
            root.transform.Find("Panel").gameObject.SetActive(false);
            root.transform.Find("Field").gameObject.SetActive(false);
            RectTransform overlay = (RectTransform)root.transform.Find("Board Overlay");
            SetRect(overlay, 0, 0, 1920, 1080);
            screen.Board.enabled = false; screen.Board.raycastTarget = false;
            screen.HandDropArea.gameObject.SetActive(false);
            foreach (SpellHandCardView card in screen.Hand) card.gameObject.SetActive(false);
            SetRect((RectTransform)screen.PlayerStatus.transform, 60, -520, 320, 460);
            Transform player = screen.PlayerStatus.transform;
            RectTransform portrait = (RectTransform)player.Find("Portrait");
            portrait.anchoredPosition = Vector2.zero; portrait.localScale = Vector3.one * 2.7f;
            SetRect((RectTransform)player.Find("Name"), 12, -300, 270, 30);
            SetRect((RectTransform)player.Find("Health"), 12, -347, 280, 28);
            SetRect((RectTransform)player.Find("Shield"), 12, -392, 26, 26);
            SetRect((RectTransform)player.Find("Defense"), 46, -390, 220, 30);
            SetRect((RectTransform)screen.DrawPile.transform, 1780, -900, 104, 150);
            SetRect((RectTransform)screen.DiscardPile.transform, 1360, -900, 104, 150);
            SetRect((RectTransform)screen.EndTurn.transform, 1610, -450, 280, 80);
            SetRect(screen.Incoming.rectTransform, 1570, -410, 310, 30);
            SetRect((RectTransform)root.transform.Find("Incoming Icon"), 1540, -415, 22, 22);
            screen.BoardCount.gameObject.SetActive(false);
            root.transform.Find("Board Count").gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/UI/SpellCombatScreen.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        SpellGameUI ui = Object.FindFirstObjectByType<SpellGameUI>(FindObjectsInactive.Include);
        SpellTable3D table = ui.GetComponent<SpellTable3D>();
        if (table == null) table = ui.gameObject.AddComponent<SpellTable3D>();
        SerializedObject config = new SerializedObject(table);
        config.FindProperty("_pieceMaterial").objectReferenceValue = piece;
        config.FindProperty("_lineMaterial").objectReferenceValue = line;
        config.ApplyModifiedPropertiesWithoutUndo();
        SerializedObject controller = new SerializedObject(ui);
        controller.FindProperty("_table").objectReferenceValue = table; controller.ApplyModifiedPropertiesWithoutUndo();
        controller.FindProperty("_background").objectReferenceValue = ((GameObject)controller.FindProperty("_canvas").objectReferenceValue).transform.Find("Background").gameObject;
        controller.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(piece); EditorUtility.SetDirty(table); EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        EditorSceneManager.SaveScene(ui.gameObject.scene); AssetDatabase.SaveAssets();
        return "Combat prefab, table materials and SampleScene references saved.";
    }

    /// <summary>기존 재질을 재사용하거나 지정 셰이더의 재질 자산을 생성해 반환한다.</summary>
    private static Material MaterialAsset(string path, string shader)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(material, path); }
        return material;
    }

    /// <summary>UI 요소의 왼쪽 위 기준 위치와 크기를 지정한다.</summary>
    private static void SetRect(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
    }
}
