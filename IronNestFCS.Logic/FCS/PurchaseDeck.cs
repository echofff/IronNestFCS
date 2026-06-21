using Il2Cpp;
using MelonLoader;
using UnityEngine;
using System.Collections;

namespace IronNestFCS.Logic.FCS;

public class PurchaseDeck {
    private Transform? heCard;
    private Transform? apCard;
    private Transform? starCard;
    private Transform? smkCard;
    private Transform? hcheCard;
    private LookAtTarget? buyButton;
    
    
    public bool TryBind() {
        var requisitionConsole = GameObject.Find("Requisition Console").transform;
        var cards = requisitionConsole.GetComponentsInChildren<PunchcardRuntime>();
        foreach (var card in cards) {
            MelonLogger.Msg($"[FCS] PurchaseDeck: 找到卡牌 {card.CurrentDefinition.ID}");
            switch (card.CurrentDefinition.ID) {
                case "HEShell":
                    heCard = card.transform;
                    break;
                case "APShell":
                    apCard = card.transform;
                    break;
                case "STARShell":
                    starCard = card.transform;
                    break;
                case "SMOKEShell":
                    smkCard = card.transform;
                    break;
                case "HCHEShell":
                    hcheCard = card.transform;
                    break;
                default:
                    break;
            }
        }
        
        buyButton = requisitionConsole.FindChild("Universal Button").GetComponent<LookAtTarget>();
        
        return true;
    }
    
    private DialInteractable GetLeftRightDial() {
        var consoleBox = GameObject.Find("Console Box").transform;
        return  consoleBox.GetComponentInChildren<DialInteractable>();
    }

    public IEnumerator BuyShell(BulletType type, LeftRight leftRight) {
        Transform? card = type switch {
            BulletType.AP => apCard,
            BulletType.HE => heCard,
            BulletType.STAR => starCard,
            BulletType.SMK => smkCard,
            BulletType.HCHE => hcheCard,
            _ => null
        };
        if (card == null) {
            MelonLogger.Error($"[FCS] BuyShell: 找不到 {type} 卡牌");
            yield break;
        }
        var target = new Vector3(6.4814f, -2.4675f, -22.0968f);
        card.position = target;
        card.GetComponent<DraggableItem>().MoveToSlot();
        yield return new WaitForSeconds(1f);
        
        switch (leftRight) {
            case LeftRight.Left:
                GetLeftRightDial().SetDialValue(0);
                break;
            case LeftRight.Right:
                GetLeftRightDial().SetDialValue(1);
                break;
        }
        yield return new WaitForSeconds(1f);
        buyButton?.OnClickDown();
        yield return new WaitForSeconds(1f);
    }
    
}