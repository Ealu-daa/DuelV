using System.Collections;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;

public enum ActionType { Attack, Skill, Ultimate, Defense, Swap }

[System.Serializable]
public class QueuedAction
{
    public CharacterState actor;
    public ActionType actionType;
    public CharacterState target; // 対象が要らない行動(防御など)はnullでもOK
    public Team targetTeam;  // 対象がどちらのチームのFWにいたか(交代で入れ替わっても追跡するため)
    public int targetSlot = -1;   // 対象を選んだ時点でのFW枠番号(対象なしなら-1のまま)
}