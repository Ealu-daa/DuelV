using System;
using UnityEngine;

public delegate void SkillAction(CharacterState caster, CharacterState target);

[CreateAssetMenu(fileName = "CharacterData", menuName = "DuelV/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("基本情報")]
    public string characterName;
    public CharacterId id;
    public int RoleGroup => (int)id / 1000;
    public string origin; // 出身国(図鑑の詳細表示用)
    public int price; // 図鑑・ショップでの購入価格(エコー)。未設定なら0
    [TextArea] public string description;

    [Header("ステータス")]
    public int maxHP;
    public int attack;
    public int defense;
    public int maxUltGauge;

    [Header("スキル")]
    public string skillName;
    [TextArea] public string skillDescription;

    [Header("ウルト")]
    public string ultName;
    [TextArea] public string ultDescription;

    [Header("テクスチャー")]
    public Sprite characterSprite;
    public Sprite characterIconSprite;

    [Header("状態別スプライト(Morphe用。未設定ならcharacterSpriteのまま)")]
    public Sprite suspendedAnimationSprite; // 仮死状態
    public Sprite revivedSprite;            // 仮死から生き返った後

    [Header("アニメーション")]
    [SerializeField] public RuntimeAnimatorController attackAnimatorController;
    [SerializeField] public AnimationClip attackAnimationClip;
    [SerializeField] public RuntimeAnimatorController skillAnimatorController;
    [SerializeField] public AnimationClip skillAnimationClip;
    [SerializeField] public Sprite skillSprite;
    [SerializeField] public RuntimeAnimatorController ultAnimatorController;
    [SerializeField] public AnimationClip ultAnimationClip;
    [SerializeField] public Sprite ultSprite;

    [Header("効果音")]
    public AudioClip attackSe;
    public AudioClip skillSe;
    public AudioClip ultSe;

    [NonSerialized] public SkillAction skillAction;
}