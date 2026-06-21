namespace IronNestFCS.Logic.FCS;

public enum Progress {
    Pending,
    Calculating,
    SelectingBullet,
    LoadingBullet,
    LoadingPowder,
    WaitLoadingFinished,
    Aiming,
    WaitingForFire,
    WaitingForBackToIdle,
    Finished,
    Failed,
}

public class ArtilleryTask {
    public float angel;
    public float distance;
    public BulletType bulletType;
    public Progress progress;
}