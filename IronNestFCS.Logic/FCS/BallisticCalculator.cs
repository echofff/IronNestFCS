using System.Collections;
using Il2Cpp;
using UnityEngine;

namespace IronNestFCS.Logic.FCS;

public class BallisticCalculator {
    private DialInteractable? distanceDial;
    private DialInteractable? chargeDial;
    private DialInteractable? directionDial;
    private DialInteractable? shellDial;
    private LookAtTarget? calculateButton;
    private OdometerDisplay? elevationDisplay;

    public bool TryBind() {
        distanceDial = GameObject.Find("Balistic Calculator Controls").
            transform.FindChild(".Range Dial Parent")
            .GetComponentInChildren<DialInteractable>();
        chargeDial = GameObject.Find("Balistic Calculator Controls").
            transform.FindChild(".Charge Dial Parent")
            .GetComponentInChildren<DialInteractable>();
        directionDial = GameObject.Find(".Gross Range Dial")
            .GetComponentInChildren<DialInteractable>();
        calculateButton = GameObject.Find("Calculate Universal Button")
            .GetComponent<LookAtTarget>();
        elevationDisplay = GameObject.Find("Odomiter Output Elivation")
            .GetComponent<OdometerDisplay>();
        shellDial = GameObject.Find(".Shell Dial")
            .GetComponent<DialInteractable>();
        return true;
    }
    
    public IEnumerator SetDistance(float distance) {
        distanceDial?.SetDialValue(distance);
        yield return new WaitForSeconds(0.5f);
    }
    
    public IEnumerator SetCharge(float charge) {
        chargeDial?.SetDialValue(charge);
        yield return new WaitForSeconds(0.5f);
    }

    public IEnumerator SetDirection(float angle) {
        directionDial?.SetDialValue(angle);
        yield return new WaitForSeconds(0.5f);
    }

    public IEnumerator SetShellType(BulletType type) {
        shellDial?.SetDialValue((float)type);
        yield return new WaitForSeconds(0.5f);
    }

    public IEnumerator Calculate() {
        calculateButton?.OnClickDown();
        yield return new WaitForSeconds(0.5f);
    }
    
    public float GetElevation() {
        return elevationDisplay?.currentNumber ?? 0;
    }

    public int MinimumCharge(float distance) {
        // var distance = distanceDial.accumulatedValue;
        return distance switch {
            < 5.0f => 1,
            < 10.0f => 2,
            < 15.0f => 3,
            < 20.0f => 4,
            < 25.0f => 5,
            _ => 6
        };
    }
    
}