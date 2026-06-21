using System.Collections;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace IronNestFCS.Logic.FCS;

public class Turret {
    private TurretController? turret;


    public bool TryBind() {
        var turretObj = GameObject.Find("TurretSystem");
        if (turretObj == null) {
            MelonLogger.Error("[FCS] Aiming: 找不到 TurretSystem");
            return false;
        }
        turret = turretObj.GetComponent<TurretController>();
        return true;
    }
    
    public IEnumerator SetRotation(float angle) {
        if (turret == null) {
            MelonLogger.Error("[FCS] Aiming: 没有绑定 TurretController");
            yield break;
        }

        turret.DesiredRotation = -angle;
        yield return new WaitForSeconds(1f);
        while (turret.IsMoving) {
            yield return new WaitForSeconds(1f);
        }
    }
    
}