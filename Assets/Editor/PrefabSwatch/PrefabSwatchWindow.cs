
using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine.SceneManagement;
using UnityEditor.Events;
using UnityEditor.Experimental.SceneManagement;

public class PrefabSwatchWindow : EditorWindow
{

    static KeyCode globalKeyDown;

    static EditorWindow win;

    static void SetEditorGlobalKeyPress()
    {
        globalKeyDown = Event.current.keyCode;
    }

    [MenuItem("Window/Prefab Swatch...", priority = 10001)]
    static void CreateWindow()
    {
        
        System.Reflection.FieldInfo info = typeof(EditorApplication).GetField("globalEventHandler", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        EditorApplication.CallbackFunction value = (EditorApplication.CallbackFunction)info.GetValue(null);
        value += SetEditorGlobalKeyPress;
 
        info.SetValue(null, value);
        
        PrefabSwatchWindow window = GetWindow<PrefabSwatchWindow>("Prefab Swatch");
        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Gizmos/PrefabSwatch Icon.png");
        window.titleContent = new GUIContent("Prefab Swatch", icon);
        window.Show();

        win = window;

        EditorApplication.update -= UpdateState;
		EditorApplication.update += UpdateState;

    }

    Transform parentTo;
    Transform selectedParent;

    bool snap = true;
    Vector2 snapCellSize = new Vector2(1, 1);

    string nameSuffix = "";

    bool continuousPlacement = false;
    bool deleteOverlapping = false;

    bool overrideOptions = false;

    bool overrideScale = false;
    Vector3 defaultScale = new Vector3(1, 1, 1);

    bool overrideRotation = false;
    Vector3 defaultRotation = new Vector3(0, 0, 0);

    bool overrideZAxis = false;
    float defaultZ = 0;

    bool overrideColor = false;
    Color defaultColor = new Color(1, 1, 1, 1);

    bool overrideMaterial = false;
    Material defaultMaterial;

    bool overrideSorting = false;

    int defaultSortingLayerID;
    int defaultSortingOrder = 0;

    int defaultSortingLayerIDSelected;

    bool restrictDeleteToLayer = false;

    int deleteSortingLayerID;
    int deleteSortingOrder = 0;

    int deleteSortingLayerIDSelected;

    Color originalColor;

    int currentUniqueID;

    Vector2 mousePos = Vector2.zero;

    float listPreviewZoom = 4.0f;

    List<GameObject> hasNotBoxCollider = new List<GameObject>();

    static GameObject selectedPrefab;

    GameObject prefabToRemove;

    Vector2 prefabsScrollView;
    Vector2 mousePosition;

    static GameObject prefabToPlaceInScene;

    Vector3 positionToPlaceAt;

    List<GameObject> prefabs = new List<GameObject>();

    PrefabSwatchAsset swatch;
    List<PrefabSwatchAsset> swatches = new List<PrefabSwatchAsset>();
    string[] swatchNames;

    string[] layerNames;
    int[] layerID;

    void LoadSwatches() {

        swatches.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:PrefabSwatchAsset"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            PrefabSwatchAsset sw = AssetDatabase.LoadAssetAtPath<PrefabSwatchAsset>(path);
            if (sw != null) {
                swatches.Add(sw);
            }
        }

        swatchNames = new string[swatches.Count];

        for (int i = 0; i < swatches.Count; ++i) {
            swatchNames[i] = swatches[i].name;
        }
        
        if (swatch != null && !swatches.Contains(swatch)) {
            swatch = null;
        }

        if (swatch == null && swatches.Count > 0) {
            swatch = swatches[0];
        }

    }


    void OnSelectionChange()
    {
        LoadSwatches();
    }


    void OnEnable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;

        Undo.undoRedoPerformed -= Repaint;
        Undo.undoRedoPerformed += Repaint;

        wantsMouseMove = true;
        wantsMouseEnterLeaveWindow = true;

        LoadSwatches();

        layerNames = GetSortingLayerNames();
        layerID = GetSortingLayerUniqueIDs();

        defaultSortingLayerID = SortingLayer.GetLayerValueFromName("Default");

        snapCellSize.x = EditorPrefs.GetFloat("Editor_PrefabSwatch_SnapX");
        snapCellSize.y = EditorPrefs.GetFloat("Editor_PrefabSwatch_SnapY");


    }

    static void DeselectPrefab()
    {

        EditorApplication.delayCall += () => {
            selectedPrefab = null;
            win?.Repaint();
            Tools.current = Tool.Move;
        };

    }

    static void UpdateState()
    {

        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage != null) {

            EditorApplication.delayCall += () => {
                DeselectPrefab();
                ClearPlacingPrefab();
            };
        }


        if (globalKeyDown == KeyCode.Escape) {
            DeselectPrefab();
            ClearPlacingPrefab();
            globalKeyDown = KeyCode.None;
        }

    }

    void OnGUI()
    {

        if (defaultMaterial == null) {
            defaultMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        EditorGUILayout.Space();

        if (swatch == null) {
            LoadSwatches();
            EditorGUILayout.HelpBox("There are no Prefab Swatches available", MessageType.Warning);
            if (GUILayout.Button("Create New Swatch")) {
                EditorApplication.ExecuteMenuItem("Assets/Create/Prefab Swatch");
            }
            return;
        }

        if (Event.current.isMouse) {
            mousePosition = Event.current.mousePosition;
            Repaint();
        }

        EditorGUILayout.Space();

        GUIStyle guiSeparator = new GUIStyle("box");

        // parent to
        selectedParent = EditorGUILayout.ObjectField("Parent To", parentTo, typeof(Transform), true) as Transform;
        if (selectedParent != parentTo) {
            if (selectedParent == null || (PrefabUtility.GetCorrespondingObjectFromSource(selectedParent) == null && PrefabUtility.GetPrefabInstanceHandle(selectedParent) == null)) {
                parentTo = selectedParent;
                selectedParent = null;
            }
        }

        overrideOptions = EditorGUILayout.Foldout(overrideOptions, "Options");
        if (overrideOptions) {

        EditorGUILayout.Space();

        // name suffix
        nameSuffix = EditorGUILayout.TextField("Name Suffix", nameSuffix);

        EditorGUILayout.Space();

        // scale
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Override Scale");
        overrideScale = EditorGUILayout.Toggle(overrideScale, GUILayout.Width(15f));
        EditorGUILayout.EndHorizontal();
        if (overrideScale) {
            defaultScale = EditorGUILayout.Vector3Field("", defaultScale);
        }

        // rotation
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Override Rotation");
        overrideRotation = EditorGUILayout.Toggle(overrideRotation, GUILayout.Width(15f));
        EditorGUILayout.EndHorizontal();
        if (overrideRotation) {
            defaultRotation = EditorGUILayout.Vector3Field("", defaultRotation);
        }

        // position z
        EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Override Z Axis");
            overrideZAxis = EditorGUILayout.Toggle(overrideZAxis, GUILayout.Width(15f));
        EditorGUILayout.EndHorizontal();
        if (overrideZAxis) {
            defaultZ = EditorGUILayout.FloatField("", defaultZ);
        }

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        // sorting layer
        layerNames = GetSortingLayerNames();
        layerID = GetSortingLayerUniqueIDs();

        EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Override Sorting");
            overrideSorting = EditorGUILayout.Toggle(overrideSorting, GUILayout.Width(15f));
        EditorGUILayout.EndHorizontal();

        if (overrideSorting) {

            EditorGUILayout.BeginVertical();

                if (!SortingLayer.IsValid(defaultSortingLayerID)) {
                    defaultSortingLayerID = SortingLayer.GetLayerValueFromName("Default");
                    defaultSortingLayerIDSelected = 0;
                }

                defaultSortingLayerIDSelected = EditorGUILayout.Popup("Sorting Layer", defaultSortingLayerIDSelected, layerNames);
                defaultSortingLayerID = layerID[defaultSortingLayerIDSelected];

                defaultSortingOrder = EditorGUILayout.IntField("Order in Layer", defaultSortingOrder);
            EditorGUILayout.EndVertical();

        }

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        // color
        EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Override Color");
            overrideColor = EditorGUILayout.Toggle(overrideColor, GUILayout.Width(15f));
        EditorGUILayout.EndHorizontal();
        if (overrideColor) {
            defaultColor = EditorGUILayout.ColorField("Color Tint", defaultColor);
        }

        // material
        EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Override Material");
            overrideMaterial = EditorGUILayout.Toggle(overrideMaterial, GUILayout.Width(15f));
        EditorGUILayout.EndHorizontal();
        if (overrideMaterial) {
            defaultMaterial = (Material)EditorGUILayout.ObjectField("Material", defaultMaterial, typeof(Material), false);
        }

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        }

        // --- separator ---
        guiSeparator.border.top     = guiSeparator.border.bottom  = 1;
        guiSeparator.margin.top     = guiSeparator.margin.bottom  = 5;
        guiSeparator.margin.bottom  = guiSeparator.margin.top     = 5;
        guiSeparator.padding.top    = guiSeparator.padding.bottom = 1;
        GUILayout.Box("", guiSeparator, GUILayout.ExpandWidth(true), GUILayout.Height(1));

        // pixel snap
        EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Snap");
            GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", "Enabling this option will snap the Objects to the specified Cell size (in Units)"));
            snap = EditorGUILayout.Toggle(snap, GUILayout.Width(15f));
        EditorGUILayout.EndHorizontal();
            if (snap) {
            snapCellSize = EditorGUILayout.Vector2Field("", snapCellSize);
            snapCellSize = Vector2.Max(snapCellSize, new Vector2(0.1f, 0.1f));
            }
        

        EditorGUILayout.Space();

        // continuous placement
        continuousPlacement = EditorGUILayout.Toggle("Continuous Placement", continuousPlacement);
        GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", "Enabling this option will continuously place Prefabs when you click and drag the mouse"));

        // delete overlapping
        deleteOverlapping = EditorGUILayout.Toggle("Delete Overlapping", deleteOverlapping);
        GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", "Enabling this option will delete any GameObjects with the same name as the one placed when overlapping. If the GameObject is missing a BoxCollider2D Component, it will be auto-added. Deactivate this option or add a BoxCollider2D Component yourself if you don't want this behavior"));

        EditorGUILayout.Space();

        // delete on sorting layer
        restrictDeleteToLayer = EditorGUILayout.Toggle("Restrict Delete", restrictDeleteToLayer);
        GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent("", "Enabling this option will restrict the deletion functionality with right click only to a specific sorting layer and order in that layer"));

        if (restrictDeleteToLayer) {

            EditorGUILayout.BeginVertical();

            if (!SortingLayer.IsValid(deleteSortingLayerID)) {
                deleteSortingLayerID = SortingLayer.GetLayerValueFromName("Default");
                deleteSortingLayerIDSelected = 0;
            }

            deleteSortingLayerIDSelected = EditorGUILayout.Popup("Sorting Layer", deleteSortingLayerIDSelected, layerNames);
            deleteSortingLayerID = layerID[deleteSortingLayerIDSelected];

            deleteSortingOrder = EditorGUILayout.IntField("Order in Layer", deleteSortingOrder);

            EditorGUILayout.EndVertical();

        }

        EditorGUILayout.Space();

        // --- separator ---
        guiSeparator.border.top     = guiSeparator.border.bottom  = 1;
        guiSeparator.margin.top     = guiSeparator.margin.bottom  = 5;
        guiSeparator.margin.bottom  = guiSeparator.margin.top     = 5;
        guiSeparator.padding.top    = guiSeparator.padding.bottom = 1;
        GUILayout.Box("", guiSeparator, GUILayout.ExpandWidth(true), GUILayout.Height(1));

        EditorGUILayout.Space();

        int swatchIndex = swatches.IndexOf(swatch);
        swatchIndex = EditorGUILayout.Popup(swatchIndex, swatchNames);
        swatch = swatchIndex < 0 ? null : swatches[swatchIndex];

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        Rect    lastRect          = GUILayoutUtility.GetLastRect();
        Vector2 mouseInScrollView = mousePosition;
        mouseInScrollView.x   -= lastRect.xMin - prefabsScrollView.x;
        mouseInScrollView.y   -= lastRect.yMax - prefabsScrollView.y;

        if (swatch.prefabs.Length <= 0) {
            EditorGUILayout.HelpBox("The selected Swatch doesn't contain any Prefabs", MessageType.Warning);
            return;
        }

        // prefab list
        prefabsScrollView = EditorGUILayout.BeginScrollView(prefabsScrollView);

        float buttonHeight = EditorGUIUtility.singleLineHeight * listPreviewZoom;
        GUILayoutOption heightStyle = GUILayout.Height(buttonHeight);

        foreach (GameObject prefab in swatch.prefabs) {

            if (prefab == null) {
                continue;
            }

            if (listPreviewZoom > 999) {

                /* Thumbnail View */
                
                // todo

            } else {

                /* List View */
                Rect rect = EditorGUILayout.GetControlRect(heightStyle);
            
                Rect highlightRect = rect;
                highlightRect.x -= 1f;
                highlightRect.y -= 1f;
                highlightRect.width += 2f;
                highlightRect.height += 2f;

                // selected highlight
                if (prefab == selectedPrefab) {
                    
                    EditorGUI.DrawRect(highlightRect, new Color32(0x42, 0x80, 0xe4, 0xff));

                } else {

                    EditorGUIUtility.AddCursorRect(highlightRect, MouseCursor.Link);

                    if (highlightRect.Contains(mouseInScrollView)) {

                        EditorGUI.DrawRect(highlightRect, new Color32(0x42, 0x80, 0xe4, 0x40));

                        if (Event.current.type == EventType.MouseDown) {
                            EditorApplication.delayCall += () => {

                                selectedPrefab = prefab;
                                Tools.current = Tool.None;
                                SceneView.RepaintAll();

                            };
                        }
                    }

                }

                // prefab icon
                Rect iconRect = new Rect(rect.x, rect.y, rect.height, rect.height);
                Texture2D icon = AssetPreview.GetAssetPreview(prefab);

                if (icon != null) {
                    GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true, 1f, Color.white, Vector4.zero, Vector4.one * 4f);
                } else {
                    EditorGUI.DrawRect(iconRect, EditorStyles.label.normal.textColor * 0.25f);
                }

                // prefab name label
                Rect labelRect = rect;
                labelRect.x += iconRect.width + 4f;
                labelRect.width -= iconRect.width + 4f;
                labelRect.height = EditorGUIUtility.singleLineHeight;
                labelRect.y += (buttonHeight - labelRect.height) * 0.5f;
                GUIStyle labelStyle = EditorStyles.label;
                GUI.Label(labelRect, Regex.Replace(prefab.name, "(?!^)([A-Z])", " $1"), labelStyle);

            }

            EditorPrefs.SetFloat("Editor_PrefabSwatch_SnapX", snapCellSize.x);
            EditorPrefs.SetFloat("Editor_PrefabSwatch_SnapY", snapCellSize.y);

            

        }

        EditorGUILayout.Space();

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();

        // --- separator ---
        guiSeparator.border.top     = guiSeparator.border.bottom  = 1;
        guiSeparator.margin.top     = guiSeparator.margin.bottom  = 5;
        guiSeparator.margin.bottom  = guiSeparator.margin.top     = 5;
        guiSeparator.padding.top    = guiSeparator.padding.bottom = 1;
        GUILayout.Box("", guiSeparator, GUILayout.ExpandWidth(true), GUILayout.Height(1));

        EditorGUILayout.BeginHorizontal();

        GUI.enabled = (selectedPrefab != null);
        if (GUILayout.Button(new GUIContent("Stop Editing", "Stop placing Prefabs (ESC Key)"))) {
            DeselectPrefab();
        }
        GUI.enabled = true;

        // list view zoom
        listPreviewZoom = EditorGUILayout.Slider(listPreviewZoom, 1f, 10f);

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        if (AssetPreview.IsLoadingAssetPreviews()) {
            Repaint();
        }

    }


    static void ClearPlacingPrefab()
    {
        if (prefabToPlaceInScene != null) {
            DestroyImmediate(prefabToPlaceInScene);
            prefabToPlaceInScene = null;
        }
    }


    void PlacePrefab()
    {
        if (prefabToPlaceInScene == null)
            return;

        Transform t = prefabToPlaceInScene.transform;
        prefabToPlaceInScene.hideFlags = HideFlags.None;

        if (prefabToPlaceInScene.GetComponent<SpriteRenderer>() != null) {
            prefabToPlaceInScene.GetComponent<SpriteRenderer>().color = originalColor;
        }

        if (nameSuffix != "") {
            prefabToPlaceInScene.gameObject.name = prefabToPlaceInScene.gameObject.name+nameSuffix;
        }

        prefabToPlaceInScene = null;
        UpdatePlacingPrefab();

        Undo.RegisterCreatedObjectUndo(prefabToPlaceInScene, "Place Prefab \""+Regex.Replace(selectedPrefab.name, "(?!^)([A-Z])", " $1")+"\"");

        if (parentTo != null) {
            Vector3    pos = t.localPosition;
            Quaternion rot = t.localRotation;
            t.parent = parentTo;
            t.position = pos;
            t.rotation = rot;
        }

    }


    void RemovePrefab() {

        Vector2 positionToRemoveAt = mousePosition;

        GameObject prefabToRemove = HandleUtility.PickGameObject(positionToRemoveAt, true);

        if (prefabToRemove != null && prefabToRemove.GetComponent<Camera>() == null) {
            
            if (restrictDeleteToLayer) {

                if (
                    prefabToRemove.GetComponent<SpriteRenderer>().sortingLayerID == deleteSortingLayerID &&
                    prefabToRemove.GetComponent<SpriteRenderer>().sortingOrder == deleteSortingOrder
                    ) {
                    DestroyImmediate(prefabToRemove);
                    prefabToRemove = null;
                }

            } else {
            
                DestroyImmediate(prefabToRemove);
                prefabToRemove = null;

            }

        }

    }


    void RemoveOverlappedPrefab() {

        if (!deleteOverlapping) {
            return;
        }

        var ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        var hits = Physics2D.GetRayIntersectionAll(ray, float.MaxValue);
        var nearest = new RaycastHit2D();

        GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>() ;

        foreach(GameObject go in allObjects)
        {

            if (go.GetComponent<BoxCollider2D>() == null) {
                go.AddComponent<BoxCollider2D>();
                hasNotBoxCollider.Add(go);
            }

        }  

        if (hits.Length > 0)
        {
            float nearestDist = float.MaxValue;
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider.gameObject == prefabToPlaceInScene || hit.collider.transform.IsChildOf(prefabToPlaceInScene.transform) || hit.collider.gameObject == Camera.main) {
                   
                    continue;
                }
                if (hit.distance < nearestDist)
                {
                    nearestDist = hit.distance;
                    nearest = hit;
                    DestroyImmediate(hit.collider.gameObject);
                    
                }
            }
        }


    }


    void UpdatePlacingPrefab()
    {

        if (prefabToPlaceInScene != null) {
            GameObject prefab = (GameObject)PrefabUtility.GetCorrespondingObjectFromSource(prefabToPlaceInScene);
            if (selectedPrefab != prefab) {
                ClearPlacingPrefab();
            }
        }

        if (prefabToPlaceInScene == null && selectedPrefab != null) {

            prefabToPlaceInScene = (GameObject)PrefabUtility.InstantiatePrefab(selectedPrefab, SceneManager.GetActiveScene());
            prefabToPlaceInScene.hideFlags = HideFlags.HideAndDontSave | HideFlags.NotEditable;

            if (prefabToPlaceInScene.GetComponent<SpriteRenderer>() != null) {

                originalColor = prefabToPlaceInScene.GetComponent<SpriteRenderer>().color;
            
                prefabToPlaceInScene.GetComponent<SpriteRenderer>().color = new Color(
                    prefabToPlaceInScene.GetComponent<SpriteRenderer>().color.r,
                    prefabToPlaceInScene.GetComponent<SpriteRenderer>().color.g,
                    prefabToPlaceInScene.GetComponent<SpriteRenderer>().color.b,
                    0.6f
                );

            }
        }

        if (prefabToPlaceInScene == null) {
            return;
        }

        positionToPlaceAt = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition).origin;

        if (snap && (snapCellSize.x > 0f && snapCellSize.y > 0f)) {
            positionToPlaceAt.x = Mathf.Round(positionToPlaceAt.x / snapCellSize.x) * snapCellSize.x;
            positionToPlaceAt.y = Mathf.Round(positionToPlaceAt.y / snapCellSize.y) * snapCellSize.y;

        }

        Quaternion rot = Quaternion.identity;

        if (overrideZAxis) {
            prefabToPlaceInScene.transform.localPosition = new Vector3(positionToPlaceAt.x, positionToPlaceAt.y, defaultZ);
        } else {
            prefabToPlaceInScene.transform.localPosition = new Vector3(positionToPlaceAt.x, positionToPlaceAt.y, selectedPrefab.transform.position.z);
        }

        if (overrideRotation) {
            prefabToPlaceInScene.transform.localRotation = Quaternion.Euler(defaultRotation);
        } else {
            prefabToPlaceInScene.transform.localRotation = selectedPrefab.transform.localRotation;
        }

        if (overrideScale) {
            prefabToPlaceInScene.transform.localScale = defaultScale;
        } else {
            prefabToPlaceInScene.transform.localScale = selectedPrefab.transform.localScale;
        }

        SpriteRenderer spr = prefabToPlaceInScene.GetComponent<SpriteRenderer>();
        if (spr != null) {

            if (overrideColor) {
                originalColor = defaultColor;
            }
            if (overrideMaterial) {
                spr.material = defaultMaterial;
            }

            if (overrideSorting) {
                spr.sortingLayerID = defaultSortingLayerID;
                spr.sortingOrder   = defaultSortingOrder;
            }
        }

    }


    void OnSceneGUI(SceneView view)
    {

        view.wantsMouseMove = true;
        view.wantsMouseEnterLeaveWindow = true;

        if (selectedPrefab == null) {
            ClearPlacingPrefab();
            return;
        }

        int control = GUIUtility.GetControlID(FocusType.Passive);

        HandleUtility.Repaint();
        Handles.color = Color.yellow;


        if (Event.current.isMouse) {
            mousePosition = Event.current.mousePosition;
        }
            
        if (Event.current.type == EventType.MouseLeaveWindow) {
            ClearPlacingPrefab();
        } else if (Event.current.isMouse || Event.current.type == EventType.MouseEnterWindow) {
            UpdatePlacingPrefab();
        }

        switch (Event.current.type)
        {
            case EventType.Layout:

                HandleUtility.AddDefaultControl(control);

                break;

            case EventType.MouseDown:

                if (Event.current.button == 0) {
                    Tools.current = Tool.None;
                    PlacePrefab();
                    RemoveOverlappedPrefab();
                    Event.current.Use();
                    
                }
                else if (Event.current.button == 1) {
                    RemovePrefab();
                    Event.current.Use();
                }

                break;

            case EventType.MouseDrag:

                if (Event.current.button == 0 && continuousPlacement) {
                    
                    Tools.current = Tool.None;
                    PlacePrefab();
                    RemoveOverlappedPrefab();
                    Event.current.Use();
                }
                else if (Event.current.button == 1 && continuousPlacement) {
                    RemovePrefab();
                    Event.current.Use();
                }

            break;

            case EventType.MouseUp:

                if (Event.current.button == 0) {
                    Tools.current = Tool.None;
                    Event.current.Use();
                }

                break;
        }

        if (prefabToPlaceInScene != null) {
            Handles.CircleHandleCap(control, positionToPlaceAt, Quaternion.identity, 0.05f, EventType.Repaint);
            DebugDrawBbox(prefabToPlaceInScene, Color.yellow);
        }

        Handles.BeginGUI();

        GUILayout.BeginArea(new Rect(4f, 4f, view.position.width, view.position.height));

        Rect rect = GUILayoutUtility.GetRect(300f, EditorGUIUtility.singleLineHeight);
        GUI.Label(rect, "X: " + positionToPlaceAt.x.ToString("0.00"), EditorStyles.whiteLabel);
        rect = GUILayoutUtility.GetRect(300f, EditorGUIUtility.singleLineHeight);
        rect.y -= 4f;
        GUI.Label(rect, "Y: " + positionToPlaceAt.y.ToString("0.00"), EditorStyles.whiteLabel);
        rect = GUILayoutUtility.GetRect(300f, EditorGUIUtility.singleLineHeight);
        rect.y -= 8f;
        GUI.Label(rect, "Z: " + defaultZ.ToString("0.00"), EditorStyles.whiteLabel);

        rect.y = 0;
        rect.x = rect.width-160f;
        Color defaultColor = GUI.color;
        GUI.color = Color.yellow;
        GUI.Label(rect, "ESC to exit Editing Mode", EditorStyles.whiteLabel);
        GUI.color = defaultColor;

        GUILayout.EndArea();

        Handles.EndGUI();

    }

    public string[] GetSortingLayerNames() {
        Type internalEditorUtilityType = typeof(InternalEditorUtility);
        PropertyInfo sortingLayersProperty = internalEditorUtilityType.GetProperty("sortingLayerNames", BindingFlags.Static | BindingFlags.NonPublic);
        return (string[])sortingLayersProperty.GetValue(null, new object[0]);
    }

    public int[] GetSortingLayerUniqueIDs() {
        Type internalEditorUtilityType = typeof(InternalEditorUtility);
        PropertyInfo sortingLayerUniqueIDsProperty = internalEditorUtilityType.GetProperty("sortingLayerUniqueIDs", BindingFlags.Static | BindingFlags.NonPublic);
        return (int[])sortingLayerUniqueIDsProperty.GetValue(null, new object[0]);
    }

    void DebugDrawBbox(GameObject go, Color color)
    {

        if (go == null) {
            return;
        }

        SpriteRenderer sprRenderer = go.GetComponent<SpriteRenderer>();

        if (sprRenderer == null) {
            return;
        }

        if (sprRenderer.sprite == null) {
            return;
        }

        Vector3 position           = go.transform.position;
        Vector3 scale              = go.transform.localScale;

        Vector2 anchor = new Vector2( 
                                (sprRenderer.sprite.pivot.x/sprRenderer.sprite.pixelsPerUnit)*scale.x,
                                (sprRenderer.sprite.pivot.y/sprRenderer.sprite.pixelsPerUnit)*scale.y
                            );

        Vector2 size = new Vector2(
                                (sprRenderer.sprite.texture.width/sprRenderer.sprite.pixelsPerUnit)*scale.x,
                                (sprRenderer.sprite.texture.height/sprRenderer.sprite.pixelsPerUnit)*scale.y
                            );

        // top left
        float x1 = position.x-anchor.x;
        float y1 = position.y-anchor.y+size.y;
        Vector2 topLeft = new Vector2(x1, y1);

        // top right
        float x2 = position.x-anchor.x+size.x;
        float y2 = position.y-anchor.y+size.y;
        Vector2 topRight = new Vector2(x2, y2);

        // bottom
        float x3 = position.x-anchor.x + size.x;
        float y3 = position.y-anchor.y;
        Vector2 bottomRight = new Vector2(x3, y3);

        // left
        float x4 = position.x-anchor.x;
        float y4 = position.y-anchor.y;
        Vector2 bottomLeft = new Vector2(x4, y4);


        Handles.DrawLine(topLeft, topRight);
        Handles.DrawLine(topRight, bottomRight);
        Handles.DrawLine(bottomRight, bottomLeft);
        Handles.DrawLine(bottomLeft, topLeft);

    }

    void OnDestroy()
    {
        DeselectPrefab();
        ClearPlacingPrefab();
        globalKeyDown = KeyCode.None;
    }

}
