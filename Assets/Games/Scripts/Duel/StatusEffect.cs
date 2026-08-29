using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 持続効果の入れ物(火傷、バフなど)は後で詳細を詰める前提の仮置き
[System.Serializable]
public class StatusEffect
{
    public string effectName;
    public int expiresAtHalfTurn;
    public int value;
    public bool isDebuff; // デバフ
    public Sprite icon; // 追加
    public string description;

    // 期限切れ時の処理はeffectNameから StatusEffectBehaviors.InvokeOnExpire() が名前ベースで解決する
    // (デリゲートを直接持たせるとオンライン対戦の再接続時スナップショット保存ができなくなるため)

    // DoT/HoT用: 期限が切れるまで毎ハーフターン適用される増減値(正=回復、負=ダメージ、0=無効)
    public int tickValue;

    // 誰が付与したか(例: チェインの衰弱スタック。付与者が戦闘不能になった時にリセットする用途)
    public CharacterState source;
}
