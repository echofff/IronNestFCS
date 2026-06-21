using HarmonyInstance = HarmonyLib.Harmony;
using System.Collections;
using Il2Cpp;
using IronNestFCS.Logic.FCS;
using MelonLoader;
using UnityEngine;

namespace IronNestFCS.Logic;

public enum LeftRight {
    Left,
    Right,
}

/// <summary>
/// 纯火控领域逻辑：查找游戏对象、读取游戏数据、操控游戏内交互（dial 等）。
/// 不含任何 UI / IMGUI / 生命周期框架代码——那些在 <see cref="FcsModule"/> 和 <see cref="FcsWindow"/> 里。
///
/// 重载安全规则：
///  - 不要在这里注册新的 IL2CPP 类型（同一类型进程内只能注册一次）。
///  - 每次实例用独立的 Harmony 实例；Shutdown 时 UnpatchSelf。
///  - 所有对 IL2CPP 对象的引用在 Shutdown 时清空，便于旧 ALC 回收。
/// </summary>
public class FSC
{
    private const string HarmonyId = "com.svr2kos2.ironnestfcs.logic";

    private HarmonyInstance? _harmony;
    
    private FcsSceneInteractor _sceneInteractor;
    private readonly PurchaseDeck _purchaseDeck = new();
    public readonly MapTable MapTable = new MapTable();
    public readonly BallisticCalculator BallisticCalculator = new BallisticCalculator();
    public readonly GunSystem LeftGun = new GunSystem();
    public readonly GunSystem RightGun = new GunSystem();
    public readonly Turret Turret = new Turret();
    public readonly TriggerConsole TriggerConsole = new();
    
    public ArtilleryTask? LeftTask = null;
    public ArtilleryTask? RightTask = null;

    // 正在运行的协程句柄。Dispose 时全部停掉，避免热重载后旧 ALC 的协程继续执行导致崩溃。
    private readonly List<object> _runningCoroutines = new();
    public FSC() {
        this._sceneInteractor = new FcsSceneInteractor(this);
    }

    public bool IsBound { get; private set; } = false;

    /// <summary>查找并绑定游戏对象。返回 false 表示当前场景还没有目标控件。</summary>
    public bool TryBind()
    {
        // 每次重载创建全新的 Harmony 实例，避免与上一版补丁冲突。
        _sceneInteractor = new FcsSceneInteractor(this);
        _sceneInteractor.Initialize();
        _harmony = new HarmonyInstance(HarmonyId);
        IsBound = true;
        IsBound &= MapTable.TryBind();
        IsBound &= BallisticCalculator.TryBind();
        IsBound &= LeftGun.TryBind("Left");
        IsBound &= RightGun.TryBind("Right");
        IsBound &= _purchaseDeck.TryBind();
        IsBound &= Turret.TryBind();
        IsBound &= TriggerConsole.TryBind();
        MelonLogger.Msg("[FCS] 初始化完成，已绑定 DialInteractable。");
        return true;
    }

    public void Update() {
        _sceneInteractor.Update();
    }
    
    /// <summary>释放：撤销补丁、清空 IL2CPP 引用。</summary>
    public void Dispose()
    {
        // 停掉所有未完成的协程，否则热重载后旧 ALC 的协程仍会被 Unity 驱动 → 崩溃。
        foreach (var handle in _runningCoroutines) {
            try { MelonCoroutines.Stop(handle); }
            catch (Exception ex) { MelonLogger.Error($"[FCS] 停止协程失败: {ex}"); }
        }
        _runningCoroutines.Clear();

        _sceneInteractor.ShutDown();
        try { _harmony?.UnpatchSelf(); }
        catch (Exception ex) { MelonLogger.Error($"[FCS] UnpatchSelf 失败: {ex}"); }
        _harmony = null;
    }

    /// <summary>
    /// 启动一个火控任务。用 MelonCoroutines 跑协程实现延时——
    /// 协程由 Unity 在主线程分帧驱动，yield 期间不阻塞、恢复后仍在主线程，
    /// 因此可安全访问 IL2CPP 对象。绝不能用 async/Task.Delay：其 continuation
    /// 会在线程池线程恢复，跨线程访问 IL2CPP 运行时会导致进程崩溃且无日志。
    /// </summary>
    public void RunTask(LeftRight leftRight) {
        var handle = MelonCoroutines.Start(RunTaskRoutine(leftRight));
        _runningCoroutines.Add(handle);
    }

    private IEnumerator RunTaskRoutine(LeftRight leftRight) {
        var gunSys = leftRight == LeftRight.Left ? LeftGun : RightGun;
        var task = leftRight == LeftRight.Left ? LeftTask : RightTask;
        if (task == null)
            yield break;
        
        yield return TriggerConsole.ConfirmTask();

        task.progress = Progress.Calculating;

        // calculate
        yield return BallisticCalculator.SetDistance(task.distance);
        yield return BallisticCalculator.SetDirection(task.angel);
        yield return BallisticCalculator.SetCharge(BallisticCalculator.MinimumCharge(task.distance));
        yield return BallisticCalculator.SetShellType(task.bulletType);
        yield return BallisticCalculator.Calculate();
        var elevation = BallisticCalculator.GetElevation();

        task.progress = Progress.SelectingBullet;
        
        // check shell
        if (!gunSys.HaveBulletInCylinder(task.bulletType)) {
            if (!gunSys.HaveEmptyShellInCylinder()) {
                task.progress = Progress.Failed;
                yield break;
            }
            yield return _purchaseDeck.BuyShell(task.bulletType, leftRight);
        }
        task.progress = Progress.LoadingBullet;
        yield return gunSys.LoadBullet(task.bulletType);

        yield return TriggerConsole.ConfirmBullet();
        
        // check powder
        var charge = BallisticCalculator.MinimumCharge(task.distance);
        task.progress = Progress.LoadingPowder;
        yield return gunSys.LoadPowder(charge);
        task.progress = Progress.WaitLoadingFinished;
        while (!gunSys.CanFire()) {
            yield return new WaitForSeconds(1f);
        }

        task.progress = Progress.Aiming;
        MelonLogger.Msg($"[FCS] 计算完成，目标 {task.angel}°，{task.distance}km，仰角 {elevation}°，准备瞄准");
        // 仰角和方向同时转动，等两个都到位（类似 WaitAll）。
        yield return WaitAll(gunSys.SetElevation(elevation), Turret.SetRotation(task.angel));
        yield return TriggerConsole.ConfirmRotation();
        yield return TriggerConsole.ConfirmElevation();
        yield return TriggerConsole.ReadyToFire();
        yield return TriggerConsole.Arm(leftRight);
        
        task.progress = Progress.WaitingForFire;
        yield return gunSys.WaitFire();
        task.progress = Progress.WaitingForBackToIdle;
        yield return gunSys.WaitBackToIdle();
        task.progress = Progress.Finished;
        _sceneInteractor.TaskFinished(leftRight);
    }

    
    /// <summary>
    /// 并行运行多个子协程并等全部完成（类似 Task.WhenAll）。
    /// 每个子协程各自用 MelonCoroutines.Start 启动以并发推进，再等所有都结束。
    /// 注意：这里启动的子协程不登记到 runningCoroutines —— 它由调用它的外层任务
    /// 协程 yield 驱动，外层在 Dispose 被 Stop 时本协程一并停止；子协程则随本协程
    /// 自然结束（WaitAll 退出后不再有 yield 驱动残留）。
    /// </summary>
    private IEnumerator WaitAll(params IEnumerator[] routines) {
        int remaining = routines.Length;
        foreach (var routine in routines) {
            MelonCoroutines.Start(RunThenSignal(routine, () => remaining--));
        }
        while (remaining > 0) {
            yield return null; // 每帧检查一次
        }
    }

    /// <summary>跑完 inner 后执行 onDone（用于 WaitAll 计数）。</summary>
    private IEnumerator RunThenSignal(IEnumerator inner, System.Action onDone) {
        yield return inner;
        onDone();
    }
}
