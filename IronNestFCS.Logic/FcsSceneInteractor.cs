using Il2CppTMPro;
using IronNestFCS.Logic.FCS;
using MelonLoader;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IronNestFCS.Logic;

public class FcsSceneInteractor {
    private FSC fcs;
    
    private List<GameObject> destroyOnShutdown = new();
    private readonly ClickRaycaster clicks = new();

    public BulletType leftBulletType = BulletType.HE;
    public BulletType rightBulletType = BulletType.HE;
    
    private List<GameObject> leftBulletTypeBtns = new();
    private List<GameObject> rightBulletTypeBtns = new();

    private GameObject leftTaskButton;
    private GameObject rightTaskButton;
    
    public FcsSceneInteractor(FSC fcs) {
        this.fcs = fcs;
    }
    
    public void Initialize() {
        InitializeBulletTypeButtons(leftBulletTypeBtns, -18.4181f);
        InitializeBulletTypeButtons(rightBulletTypeBtns, -18.6381f);
        InitializeArtilleryTaskButtons();
    }

    private void InitializeBulletTypeButtons(List<GameObject> buttonsList, float z) {
        float x = 0.3488f;
        foreach (BulletType type in Enum.GetValues(typeof(BulletType))) {
            // 先声明再赋值：lambda 要捕获 button，不能在其声明表达式内部引用它。
            GameObject button = null;
            button = AddButton(() => {
                if (buttonsList == leftBulletTypeBtns) {
                    leftBulletType = type;
                }
                else {
                    rightBulletType = type;
                }
                
                foreach (var btn in buttonsList) {
                    SetColor(btn, btn == button ? Color.green : Color.white);
                }
            }, type == BulletType.HE ? Color.green : Color.white);
            button.transform.position = new Vector3(x, -0.6916f, z);
            button.transform.localScale = Vector3.one * 0.02f;
            buttonsList.Add(button);
            var text = AddText(type.ToString(), 14f);
            text.transform.SetParent(button.transform, false);
            text.transform.localPosition = new Vector3(-1.9f, 0, -10.6f);
            text.transform.localScale = Vector3.one * 1.0f;
            x -= 0.05f;
        }
    }

    public void TaskFinished(LeftRight leftRight) {
        var button = leftRight == LeftRight.Left ? leftTaskButton : rightTaskButton;
        SetColor(button, Color.red);
    }

    private void InitializeArtilleryTaskButtons() {
        leftTaskButton = AddButton(() => {
            if (fcs.LeftTask != null && fcs.LeftTask.progress != Progress.Finished) {
                return;
            }
            var task = fcs.MapTable.GetMarkTarget(1);
            task.bulletType = leftBulletType;
            task.progress = Progress.Pending;
            fcs.LeftTask = task;
            SetColor(leftTaskButton, Color.gray);
            fcs.RunTask(LeftRight.Left);
        }, Color.red);
        leftTaskButton.transform.position = new Vector3(
            0.3488f - Enum.GetValues(typeof(BulletType)).Length * 0.05f, 
            -0.6916f, 
            -18.4181f
        );
        leftTaskButton.transform.localScale = Vector3.one * 0.02f;
        var leftText = AddText("Add Task", 14f);
        leftText.transform.SetParent(leftTaskButton.transform, false);
        leftText.transform.localPosition = new Vector3(-1.9f, 0, -10.6f);
        
        rightTaskButton = AddButton(() => {
            if (fcs.RightTask != null && fcs.RightTask.progress != Progress.Finished) {
                return;
            }
            var task = fcs.MapTable.GetMarkTarget(2);
            task.bulletType = rightBulletType;
            task.progress = Progress.Pending;
            fcs.RightTask = task;
            SetColor(rightTaskButton, Color.gray);
            fcs.RunTask(LeftRight.Right);
        }, Color.red);
        rightTaskButton.transform.position = new Vector3(
            0.3488f - Enum.GetValues(typeof(BulletType)).Length * 0.05f, 
            -0.6916f, 
            -18.6381f
        );
        rightTaskButton.transform.localScale = Vector3.one * 0.02f;
        var rightText = AddText("Add Task", 14f);
        rightText.transform.SetParent(rightTaskButton.transform, false);
        rightText.transform.localPosition = new Vector3(-1.9f, 0, -10.6f);
    }
    
    public void Update() {
        clicks.Update();
    }

    public void ShutDown() {
        clicks.Clear();
        foreach (var obj in destroyOnShutdown) {
            Object.Destroy(obj);
        }
    }
    
    public GameObject AddButton(Action onClick) {
        return AddButton(onClick, Color.white);
    }

    public GameObject AddButton(Action onClick, Color color) {
        // 用自带 BoxCollider 的 cube 当可点击目标，靠 ClickRaycaster 自己 raycast 检测点击，
        // 不依赖游戏的 LookAtTarget，也不注册新 IL2CPP 类型（保持可热重载）。
        var button = GameObject.CreatePrimitive(PrimitiveType.Cube);
        destroyOnShutdown.Add(button);
        var collider = button.GetComponent<Collider>();
        clicks.Register(collider, onClick);
        SetColor(button, color);
        return button;
    }

    /// <summary>
    /// 给对象的 Renderer 换上当前渲染管线（URP）的材质并设颜色。
    /// CreatePrimitive 默认用内置管线的 Standard 材质，在 URP 下 shader 无效会渲染成紫色；
    /// 这里用 URP 的 Unlit shader 重建材质（不受光照影响，纯色所见即所得）。
    /// </summary>
    public static void SetColor(GameObject go, Color color) {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null)
            return;

        var shader = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) {
            MelonLogger.Warning("[FCS] 未找到 URP shader，颜色可能不正确。");
            // 退而求其次：直接改现有材质颜色
            if (renderer.material != null)
                renderer.material.color = color;
            return;
        }

        var mat = new Material(shader);
        // URP Unlit 用 _BaseColor 控制颜色；同时设 color 兼容。
        mat.color = color;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        renderer.material = mat;
    }

    /// <summary>
    /// 在 3D 世界里创建一段文本（World Space 的 TextMeshPro，非 UGUI）。
    /// 返回 GameObject，调用方自行设 transform.position/scale。文本/字号后续可通过
    /// go.GetComponent&lt;TextMeshPro&gt;() 修改。英文数字用默认字体即可显示。
    /// </summary>
    public GameObject AddText(string text, float fontSize = 4f) {
        var go = new GameObject("FcsText");
        destroyOnShutdown.Add(go);
        go.transform.Rotate(new Vector3(90, 0, 0));
        go.transform.Rotate(new Vector3(0, 0, -90));
        var tmp = go.AddComponent<TextMeshPro>();
        // AddComponent 后 Awake 未必已执行，字体可能未自动赋值导致不渲染；
        // 显式赋默认字体（含 ASCII，英文数字足够）。
        if (tmp.font == null && TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        // 锚点设到左上角，方便从左上往下排版（Center 会以几何中心为原点）。
        // tmp.alignment = TextAlignmentOptions.MidlineLeft;
        return go;
    }
}