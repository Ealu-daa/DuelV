using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
public static class SkillRegistry
{
    public enum VisualTargetType
    {
        Actor,           // 自分自身
        OtherAllyForward,// もう一方の味方FW(自分以外のFW1体、例: フォートのツインバーデン的なもの)
        AllAllyForward,  // 味方FW全部
        Target,          // 選択した対象(既存のtarget付き処理)
        AllEnemyForward, // 敵FW全部
    }
    private class ActionInfo
    {
        public ActionUI.TargetScope scope;
        public VisualTargetType visualTarget = VisualTargetType.Target; // デフォルトは今まで通り
        public Action<CharacterState, CharacterState> execute;
    }



    private static readonly Dictionary<CharacterId, ActionInfo> skillTable = new()
    {
        {
            CharacterId.Hollow,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                visualTarget = VisualTargetType.Actor,
                execute = (actor, target) => HollowSkill(actor)
            }
        },
        {
            CharacterId.Storm,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.EnemyForward,
                visualTarget = VisualTargetType.Target,
                execute = (actor, target) => StormSkill(actor, target)
            }
        },
        {
            CharacterId.Fort,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                visualTarget = VisualTargetType.OtherAllyForward,
                execute = (actor, target) => FortSkill(actor)
            }
        },
        {
            CharacterId.Phantom,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.EnemyForward,
                visualTarget = VisualTargetType.Target,
                execute = (actor, target) => PhantomSkill(target)
            }
        },
        {
            CharacterId.Lumina,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.AllyForward,
                visualTarget = VisualTargetType.Target,
                execute = (actor, target) => LuminaSkill(target)
            }
        },
        {
            CharacterId.Chain,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.EnemyForward,
                visualTarget = VisualTargetType.Target,
                execute = (actor, target) => ChainSkill(actor, target)
            }
        },
        {
            CharacterId.Chronicle,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.EnemyForward,
                visualTarget = VisualTargetType.Target,
                execute = (actor, target) => ChronicleSkill(target)
            }
        },
        {
            CharacterId.Double,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                visualTarget = VisualTargetType.OtherAllyForward,
                execute = (actor, target) => DoubleSkill(actor)
            }
        },
        {
            CharacterId.Morphe,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                visualTarget = VisualTargetType.AllAllyForward,
                execute = (actor, target) => MorpheSkill(actor)
            }
        },
        {
            CharacterId.Blaze,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.EnemyForward,
                visualTarget = VisualTargetType.Target,
                execute = (actor, target) => BlazeSkill(actor, target)
            }
        },
        {
            CharacterId.Aegis,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                visualTarget = VisualTargetType.OtherAllyForward,
                execute = (actor, target) => AegisSkill(actor)
            }
        },
        {
            CharacterId.Reaper,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                visualTarget = VisualTargetType.Actor,
                execute = (actor, target) => ReaperSkill(actor)
            }
        },
        {
            CharacterId.Grace,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                visualTarget = VisualTargetType.Actor,
                execute = (actor, target) => GraceSkill(actor, target)
            }
        },
        {
            CharacterId.Lapse,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.EnemyForward,
                visualTarget = VisualTargetType.Target,
                execute = (actor, target) => LapseSkill(actor, target)
            }
        },
        {
            CharacterId.Aine,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.EnemyForward,
                visualTarget = VisualTargetType.Target,
                execute = (actor, target) => AineSkill(target)
            }
        },

    };

    private static readonly Dictionary<CharacterId, ActionInfo> ultimateTable = new()
    {
        {
            CharacterId.Hollow,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.EnemyForward,
                visualTarget = VisualTargetType.Target,
                execute = (actor, target) => HollowUlt(actor, target)
            }
        },

        {
            CharacterId.Storm,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                visualTarget = VisualTargetType.AllEnemyForward,
                execute = (actor, target) => StormUlt(actor)
            }
        },
        {
            CharacterId.Fort,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                visualTarget = VisualTargetType.AllAllyForward,
                execute = (actor, target) => FortUlt(actor)
            }
        },
        {
            CharacterId.Phantom,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.EnemyForward,
                visualTarget = VisualTargetType.Target,
                execute = (actor, target) => PhantomUlt(target)
            }
        },
        {
            CharacterId.Lumina,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                visualTarget = VisualTargetType.AllAllyForward,
                execute = (actor, target) => LuminaUlt(actor)
            }
        },
        {
            CharacterId.Double,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                visualTarget = VisualTargetType.Actor,
                execute = (actor, target) => DoubleUlt(actor)
            }
        },
        {
            CharacterId.Lapse,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                visualTarget = VisualTargetType.AllEnemyForward,
                execute = (actor, target) => LapseUlt(actor)
            }
        },
        {
            CharacterId.Blaze,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.EnemyForward,
                visualTarget = VisualTargetType.Target,
                execute = (actor, target) => BlazeUlt(actor, target)
            }
        },
        {
            CharacterId.Aegis,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                visualTarget = VisualTargetType.AllAllyForward,
                execute = (actor, target) => AegisUlt(actor)
            }
        },
        {
            CharacterId.Reaper,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.EnemyForward,
                visualTarget = VisualTargetType.Target,
                execute = (actor, target) => ReaperUlt(actor, target)
            }
        },
        {
            CharacterId.Grace,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                visualTarget = VisualTargetType.Actor,
                execute = (actor, target) => GraceUlt(actor, target)
            }
        },
        {
            CharacterId.Chain,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                visualTarget = VisualTargetType.AllEnemyForward,
                execute = (actor, target) => ChainUlt(actor)
            }
        },
        {
            CharacterId.Chronicle,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.EnemyForward,
                visualTarget = VisualTargetType.Target,
                execute = (actor, target) => ChronicleUlt(actor, target)
            }
        },
        {
            CharacterId.Morphe,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                visualTarget = VisualTargetType.Actor,
                execute = (actor, target) => MorpheUlt(actor)
            }
        },
        {
            CharacterId.Aine,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                visualTarget = VisualTargetType.AllEnemyForward,
                execute = (actor, target) => AineUlt(actor)
            }
        },
    };


    // --- スキル呼び出し ---
    public static void ExecuteSkill(CharacterState actor, CharacterState target)
    {
        if (skillTable.TryGetValue(actor.data.id, out var info))
            info.execute(actor, target);
        else
            Debug.LogWarning($"未実装のスキル: {actor.data.id}");
    }

    public static ActionUI.TargetScope GetSkillTargetScope(CharacterId id)
    {
        if (skillTable.TryGetValue(id, out var info)) return info.scope;
        Debug.LogWarning($"未設定のTargetScope(skill): {id}");
        return ActionUI.TargetScope.EnemyForward;
    }

    // --- ウルト呼び出し ---
    public static void ExecuteUltimate(CharacterState actor, CharacterState target)
    {
        if (ultimateTable.TryGetValue(actor.data.id, out var info))
            info.execute(actor, target);
        else
            Debug.LogWarning($"未実装のウルト: {actor.data.id}");
    }

    public static ActionUI.TargetScope GetUltimateTargetScope(CharacterId id)
    {
        if (ultimateTable.TryGetValue(id, out var info)) return info.scope;
        Debug.LogWarning($"未設定のTargetScope(ultimate): {id}");
        return ActionUI.TargetScope.EnemyForward;
    }

    //VisualTargetTypeの取得
    public static VisualTargetType GetSkillVisualTarget(CharacterId id)
    {
        if (skillTable.TryGetValue(id, out var info)) return info.visualTarget;
        return VisualTargetType.Target;
    }

    public static VisualTargetType GetUltimateVisualTarget(CharacterId id)
    {
        if (ultimateTable.TryGetValue(id, out var info)) return info.visualTarget;
        return VisualTargetType.Target;
    }

    // CommonEffectDataを元にIcon/Descriptionを自動取得してStatusEffectを作る
    // (期限切れ時の処理はStatusEffectBehaviors側がeffectNameから解決するので、ここでは渡さない)
    private static StatusEffect CreateEffect(string name, int expiresAtHalfTurn, int value = 0, bool isDebuff = false)
    {
        var entry = CommonEffectData.Instance != null ? CommonEffectData.Instance.GetEntry(name) : null;

        return new StatusEffect
        {
            effectName = name,
            expiresAtHalfTurn = expiresAtHalfTurn,
            value = value,
            isDebuff = isDebuff,
            icon = entry?.icon,
            description = entry?.description
        };
    }

    // --- 中身の実装 ---
    private static void HollowSkill(CharacterState actor)
    {
        int n = 3;
        int expireAt = TurnManager.Instance.currentHalfTurn + n;

        actor.currentAttack += 5;

        actor.AddEffect(CreateEffect("エンプティフィル", expireAt, value: 5));
    }

    private static void HollowUlt(CharacterState actor, CharacterState target)
    {
        int damage = 10;
        target.TakeDamage(damage, actor);

        int n = 2;
        int expireAt = TurnManager.Instance.currentHalfTurn + n;

        target.AddEffect(CreateEffect("行動不能", expireAt, isDebuff: true));
    }
    private static void StormSkill(CharacterState actor, CharacterState target)
    {
        int damage = Mathf.Max(actor.currentAttack - 3, 0);
        target.TakeDamage(damage, actor);

        var team = TurnManager.Instance.GetTeamOf(actor);
        var aliveBackups = team.backups.FindAll(c => !c.IsDefeated);

        if (aliveBackups.Count > 0)
        {
            var incoming = aliveBackups[Random.Range(0, aliveBackups.Count)];
            TurnManager.Instance.ExecuteSwap(actor, incoming);
        }
    }

    private static void StormUlt(CharacterState actor)
    {
        var enemyTeam = TurnManager.Instance.GetTeamOf(actor) == BattleManager.Instance.PlayerTeam
            ? BattleManager.Instance.EnemyTeam
            : BattleManager.Instance.PlayerTeam;

        foreach (var fw in enemyTeam.forwards)
        {
            if (!fw.IsTargetableBy(actor)) continue; // 他人のハンティンググラウンド対象には及ばない
            fw.TakeDamage(6, actor);
        }

        var fwList = new List<CharacterState>(enemyTeam.forwards);
        var usedIncoming = new HashSet<CharacterState>();

        foreach (var outgoing in fwList)
        {
            var aliveBackups = enemyTeam.backups.FindAll(c => !c.IsDefeated && !usedIncoming.Contains(c));
            if (aliveBackups.Count == 0) continue;

            var incoming = aliveBackups[Random.Range(0, aliveBackups.Count)];
            usedIncoming.Add(incoming);
            TurnManager.Instance.ForceSwap(outgoing, incoming);
        }
    }
    private static void FortSkill(CharacterState actor)
    {
        var team = TurnManager.Instance.GetTeamOf(actor);
        var ally = team.forwards.Find(c => c != actor);

        if (ally == null) return;

        int n = 2;
        int expireAt = TurnManager.Instance.currentHalfTurn + n;

        ally.protectedBy = actor;
        actor.isProtectingAlly = true;

        actor.AddEffect(CreateEffect("インターセプト", expireAt));
    }
    private static void FortUlt(CharacterState actor)
    {
        var team = TurnManager.Instance.GetTeamOf(actor);

        foreach (var fw in team.forwards)
        {
            if (!fw.IsTargetableBy(actor)) continue; // 他人のハンティンググラウンド対象には及ばない
            fw.AddEffect(CreateEffect("バリア", int.MaxValue));
        }
    }
    private static void PhantomSkill(CharacterState target)
    {
        int n = 2;
        int expireAt = TurnManager.Instance.currentHalfTurn + n;

        target.AddEffect(CreateEffect("サイレンス", expireAt, isDebuff: true));
    }
    private static void PhantomUlt(CharacterState target)
    {
        int n = 2;
        int expireAt = TurnManager.Instance.currentHalfTurn + n;

        target.AddEffect(CreateEffect("コンフュージョン", expireAt, isDebuff: true));
    }
    private static void LuminaSkill(CharacterState target)
    {
        target.Heal(5);
    }
    private static void LuminaUlt(CharacterState actor)
    {
        var team = TurnManager.Instance.GetTeamOf(actor);
        var allMembers = team.AllCharacters(); // FW+BK全員

        foreach (var member in allMembers)
        {
            if (!member.IsTargetableBy(actor)) continue; // 他人のハンティンググラウンド対象には及ばない
            member.Heal(8);
            member.activeEffects.RemoveAll(e => e.isDebuff);
        }
    }

    // 衰弱スタックを1つ付与し、スタック数に応じて攻撃力を下げる(上限5、最小値0)
    // チェイン(自分)が戦闘不能になると、自分が付与したスタックは全てリセットされる
    // (CharacterState.ClearSourcedEffectsがsourceを見て自動で処理する)
    private static void ChainSkill(CharacterState actor, CharacterState target)
    {
        target.TakeDamage(2, actor);

        int currentStacks = target.activeEffects.FindAll(e => e.effectName == "衰弱スタック" && e.source == actor).Count;
        if (currentStacks >= 5) return;

        target.currentAttack = Mathf.Max(0, target.currentAttack - 1);
        var stack = CreateEffect("衰弱スタック", int.MaxValue, value: 1, isDebuff: true);
        stack.source = actor;
        target.AddEffect(stack);
    }

    // 狩猟印(6ht)+交代封じ(2ht)を付与する。「クロニクルと対象は互いしか攻撃できない」等の
    // 相互拘束部分はウルト側の仕様なので、スキルの効果分のみ実装
    private static void ChronicleSkill(CharacterState target)
    {
        int markExpireAt = TurnManager.Instance.currentHalfTurn + 6;
        int lockExpireAt = TurnManager.Instance.currentHalfTurn + 2;

        target.AddEffect(CreateEffect("狩猟印", markExpireAt, isDebuff: true));
        target.AddEffect(CreateEffect("交代封じ", lockExpireAt, isDebuff: true));
    }

    // もう一方の味方FWにシールドを付与(重複可能、最大12)
    private static void DoubleSkill(CharacterState actor)
    {
        var team = TurnManager.Instance.GetTeamOf(actor);
        var ally = team.forwards.Find(c => c != actor);
        if (ally == null) return;

        AddShield(ally, 3, 12);
    }

    // シールドが既にあれば上限までスタック、無ければ新規付与する共通処理
    private static void AddShield(CharacterState target, int amount, int maxValue)
    {
        var existing = target.activeEffects.Find(e => e.effectName == "シールド");
        if (existing != null)
        {
            existing.value = Mathf.Min(maxValue, existing.value + amount);
            return;
        }

        target.AddEffect(CreateEffect("シールド", int.MaxValue, value: amount));
    }

    // 攻撃力と防御力を入れ替える。再度使うか、交代でFWを離れると元に戻る
    // (交代時の巻き戻しはCharacterState.RevertTransformIfNeeded/TurnManagerの各交代処理側で行う)
    private static void DoubleUlt(CharacterState actor)
    {
        (actor.currentAttack, actor.currentDefense) = (actor.currentDefense, actor.currentAttack);
        actor.isTransformed = !actor.isTransformed;
    }

    // 敵FW2体のULTゲージを0にリセット
    private static void LapseUlt(CharacterState actor)
    {
        var enemyTeam = TurnManager.Instance.GetTeamOf(actor) == BattleManager.Instance.PlayerTeam
            ? BattleManager.Instance.EnemyTeam
            : BattleManager.Instance.PlayerTeam;

        foreach (var fw in enemyTeam.forwards)
        {
            fw.ResetUltGauge();
        }
    }

    // 相手FW1体が持ってるカタリストから1つ、持続2ht使用不可にする
    // ActionUIの2段階選択でプレイヤーが選んだ場合はactor.pendingHandpickChoiceに入っている。
    // CPUなど選択が無い場合は、未使用カタリストからランダムに選ぶ
    private static void LapseSkill(CharacterState actor, CharacterState target)
    {
        var chosen = actor.pendingHandpickChoice;
        actor.pendingHandpickChoice = null;

        if (chosen == null || chosen.isUsed || !target.catalysts.Contains(chosen))
        {
            var available = target.catalysts.FindAll(c => !c.isUsed);
            if (available.Count == 0) return; // 封じられる未使用カタリストが無ければ空振り
            chosen = available[Random.Range(0, available.Count)];
        }

        chosen.disabledUntilHalfTurn = TurnManager.Instance.currentHalfTurn + 2;
    }

    // 自分は最大HP+1・HP1回復(重ねがけ上限なし)、もう一方の味方FWは攻撃力+1(上限+2)
    private static void MorpheSkill(CharacterState actor)
    {
        actor.currentMaxHP += 1;
        actor.morpheMaxHpBonusTotal += 1; // メタモルフォシス復活時に攻撃力へ変換する分の積み上げ
        actor.Heal(1);

        var team = TurnManager.Instance.GetTeamOf(actor);
        var ally = team.forwards.Find(c => c != actor);
        if (ally == null) return;

        int stacks = ally.activeEffects.FindAll(e => e.effectName == "グロウシェル").Count;
        if (stacks >= 2) return;

        ally.currentAttack += 1;
        ally.AddEffect(CreateEffect("グロウシェル", int.MaxValue, value: 1));
    }

    // 2ダメージを与え、火傷(2ダメ×6ht)を付与する。火傷は上書き(重複しない)
    private static void BlazeSkill(CharacterState actor, CharacterState target)
    {
        target.TakeDamage(2, actor);

        int expireAt = TurnManager.Instance.currentHalfTurn + 6;

        target.activeEffects.RemoveAll(e => e.effectName == "火傷"); // 上書き: 既存の火傷は消してから付け直す

        var burn = CreateEffect("火傷", expireAt, isDebuff: true);
        burn.tickValue = -2;
        target.AddEffect(burn);
    }

    // 7ダメージ。対象が火傷中なら、残りht分の火傷ダメージを即座に発生させて火傷を解除する
    private static void BlazeUlt(CharacterState actor, CharacterState target)
    {
        target.TakeDamage(7, actor);

        var burn = target.activeEffects.Find(e => e.effectName == "火傷");
        if (burn == null) return;

        int remainingHt = Mathf.Max(0, burn.expiresAtHalfTurn - TurnManager.Instance.currentHalfTurn);
        int burstDamage = Mathf.Abs(burn.tickValue) * remainingHt;
        target.activeEffects.Remove(burn);

        if (burstDamage > 0)
            target.TakeDamage(burstDamage, actor);
    }

    // 味方(もう一方)に自分をツインバーデンの相方として設定する。持続2ht消費型、1回限り
    private static void AegisSkill(CharacterState actor)
    {
        var team = TurnManager.Instance.GetTeamOf(actor);
        var ally = team.forwards.Find(c => c != actor);
        if (ally == null) return;

        int expireAt = TurnManager.Instance.currentHalfTurn + 2;

        ally.twinBurdenPartner = actor;

        var twinBurden = CreateEffect("ツインバーデン", expireAt);
        twinBurden.source = actor; // 期限切れ時、この相手との紐付けだけを解除する(StatusEffectBehaviors側)
        ally.AddEffect(twinBurden);
    }

    // 持続4htの間、味方が被弾するたびに攻撃者へ攻撃者の攻撃力半分のダメージを反撃する
    private static void AegisUlt(CharacterState actor)
    {
        var team = TurnManager.Instance.GetTeamOf(actor);
        int expireAt = TurnManager.Instance.currentHalfTurn + 4;

        foreach (var fw in team.forwards)
        {
            if (!fw.IsTargetableBy(actor)) continue; // 他人のハンティンググラウンド対象には及ばない
            fw.AddEffect(CreateEffect("リベンジフィールド", expireAt));
        }
    }

    // 2ht前に自分が倒した敵から魂を吸収する。最大HP+1、HP全快
    private static void ReaperSkill(CharacterState actor)
    {
        int currentHalfTurn = TurnManager.Instance.currentHalfTurn;
        var enemyTeam = TurnManager.Instance.GetTeamOf(actor) == BattleManager.Instance.PlayerTeam
            ? BattleManager.Instance.EnemyTeam
            : BattleManager.Instance.PlayerTeam;

        CharacterState soul = null;
        foreach (var c in enemyTeam.AllCharacters())
        {
            if (c.defeatedBy == actor && !c.soulAbsorbed && c.defeatedAtHalfTurn == currentHalfTurn - 2)
            {
                soul = c;
                break;
            }
        }

        if (soul == null) return; // 条件を満たす魂がなければ何も起きない

        soul.soulAbsorbed = true;
        actor.currentMaxHP += 1;
        actor.currentHP = actor.currentMaxHP;
        actor.NotifyChanged();
    }

    // 自身の攻撃力+4ダメージ。このULTで撃破した場合、行動を消費せずソウルアブソーブ相当の効果を即発動する
    // 注意: 通常のスキルは「2ht前に倒した敵」が条件だが、撃破直後に即発動する関係上、
    // ここではその場で倒した相手の魂をそのまま吸収する形に簡略化している
    private static void ReaperUlt(CharacterState actor, CharacterState target)
    {
        bool wasAliveBefore = !target.IsDefeated;
        int damage = actor.currentAttack + 4;
        target.TakeDamage(damage, actor);

        if (wasAliveBefore && target.IsDefeated)
        {
            target.soulAbsorbed = true; // 通常スキルでの二重取得を防ぐ
            actor.currentMaxHP += 1;
            actor.currentHP = actor.currentMaxHP;
            actor.NotifyChanged();
        }
    }

    // 自分と味方BKを交代し、交代して出てきたキャラに通常攻撃をさせる
    // ActionUIの2段階選択でプレイヤーが選んだ場合は、交代先がactor.pendingEncoreIncoming、
    // 攻撃対象がattackTarget引数に入っている。CPUなど選択が無い場合はどちらもランダム
    private static void GraceSkill(CharacterState actor, CharacterState attackTarget)
    {
        var team = TurnManager.Instance.GetTeamOf(actor);

        var incoming = actor.pendingEncoreIncoming;
        actor.pendingEncoreIncoming = null;
        if (incoming == null || incoming.IsDefeated || !team.backups.Contains(incoming))
        {
            var aliveBackups = team.backups.FindAll(c => !c.IsDefeated);
            if (aliveBackups.Count == 0) return;
            incoming = aliveBackups[Random.Range(0, aliveBackups.Count)];
        }

        TurnManager.Instance.ExecuteSwap(actor, incoming);

        var enemyTeam = team == BattleManager.Instance.PlayerTeam
            ? BattleManager.Instance.EnemyTeam
            : BattleManager.Instance.PlayerTeam;

        var finalTarget = (attackTarget != null && attackTarget != actor && !attackTarget.IsDefeated)
            ? attackTarget
            : null;

        if (finalTarget == null)
        {
            var aliveEnemies = enemyTeam.forwards.FindAll(c => !c.IsDefeated);
            if (aliveEnemies.Count == 0) return;
            finalTarget = aliveEnemies[Random.Range(0, aliveEnemies.Count)];
        }

        TurnManager.Instance.PlayAttackAnimationOnly(incoming, finalTarget); // 通常攻撃と同じ演出を再生
        finalTarget.TakeDamage(incoming.currentAttack, incoming);
        incoming.AddUltGauge(2); // 通常攻撃扱いなのでゲージ+2
    }

    // 戦闘不能の味方1体をHP満タンで復活させる。復活ターンは行動不可
    // ActionUIで選んだ場合はtargetにその対象が入っている。CPUなど選択が無い場合(target==actorなど)は
    // 見つかった最初の戦闘不能キャラを自動で対象にする
    private static void GraceUlt(CharacterState actor, CharacterState target)
    {
        var team = TurnManager.Instance.GetTeamOf(actor);

        CharacterState revived = (target != null && target != actor && target.IsDefeated) ? target : null;

        if (revived == null)
        {
            foreach (var c in team.AllCharacters())
            {
                if (c.IsDefeated)
                {
                    revived = c;
                    break;
                }
            }
        }
        if (revived == null) return; // 戦闘不能の味方がいなければ何も起きない

        revived.currentHP = revived.currentMaxHP;
        revived.hasActedThisTurn = true; // 復活ターンは行動不可
        revived.swapHandled = false; // 再度戦闘不能になった時にまた繰り上げ判定させる
        revived.NotifyChanged();
    }

    // 持続4htの間、敵FW1体が受けたダメージの半分(切り捨て)がもう一方の敵FWにも入る
    private static void ChainUlt(CharacterState actor)
    {
        var enemyTeam = TurnManager.Instance.GetTeamOf(actor) == BattleManager.Instance.PlayerTeam
            ? BattleManager.Instance.EnemyTeam
            : BattleManager.Instance.PlayerTeam;
        int expireAt = TurnManager.Instance.currentHalfTurn + 4;

        foreach (var fw in enemyTeam.forwards)
        {
            if (!fw.IsTargetableBy(actor)) continue; // 他人のハンティンググラウンド対象には及ばない
            fw.AddEffect(CreateEffect("シンクザペイン", expireAt));
        }
    }

    // 敵FW1体を持続6ht「獲物」に指定し、互いにしか攻撃できなくする(通常攻撃のみ)。交代も封じる。
    // 狩猟印付きの相手が対象なら、その印を攻撃力-2に変換する。
    // 「他のキャラは2人を対象にできない」「新たなバフ・デバフを受けない」「防御行動を選べない」は
    // CharacterState.IsTargetableBy/各所のガード(ActionUI・CpuController・各AoE効果)で対応済み
    private static void ChronicleUlt(CharacterState actor, CharacterState target)
    {
        int expireAt = TurnManager.Instance.currentHalfTurn + 6;

        actor.huntBoundTo = target;
        target.huntBoundTo = actor;

        var markOnActor = CreateEffect("獲物指定", expireAt);
        markOnActor.source = target; // 期限切れ時、targetとの拘束だけを解除する(StatusEffectBehaviors側)
        actor.AddEffect(markOnActor);

        var markOnTarget = CreateEffect("獲物指定", expireAt);
        markOnTarget.source = actor;
        target.AddEffect(markOnTarget);

        actor.AddEffect(CreateEffect("交代封じ", expireAt, isDebuff: true));
        target.AddEffect(CreateEffect("交代封じ", expireAt, isDebuff: true));

        var mark = target.activeEffects.Find(e => e.effectName == "狩猟印");
        if (mark != null)
        {
            target.activeEffects.Remove(mark);
            target.currentAttack = Mathf.Max(0, target.currentAttack - 2);
            target.AddEffect(CreateEffect("狩猟印(攻撃力減少)", expireAt, value: 2, isDebuff: true));
        }
    }

    // 相手FW1体に持続2ht「命中率低下」(自分の行動が25%の確率で不発になる)と「防御力低下」(防御力0)を付与
    private static void AineSkill(CharacterState target)
    {
        int expireAt = TurnManager.Instance.currentHalfTurn + 2;

        target.AddEffect(CreateEffect("命中率低下", expireAt, isDebuff: true));

        int defReduction = target.currentDefense;
        target.currentDefense = 0;
        target.AddEffect(CreateEffect("防御力低下", expireAt, value: defReduction, isDebuff: true));
    }

    // 敵FW全体に持続4ht「ターゲットランダム化」を付与(対象指定効果の対象が選択可能な相手の中からランダムになる)
    private static void AineUlt(CharacterState actor)
    {
        var enemyTeam = TurnManager.Instance.GetTeamOf(actor) == BattleManager.Instance.PlayerTeam
            ? BattleManager.Instance.EnemyTeam
            : BattleManager.Instance.PlayerTeam;
        int expireAt = TurnManager.Instance.currentHalfTurn + 4;

        foreach (var fw in enemyTeam.forwards)
        {
            if (fw.IsDefeated) continue;
            fw.AddEffect(CreateEffect("ターゲットランダム化", expireAt, isDebuff: true));
        }
    }

    // 仮死状態からの復活専用(CanUseUltimateにより仮死中しか呼ばれない)。
    // 被弾分を引いた現在HPのまま復活し、スキルで積んだ最大HP増加分の合計を
    // 攻撃力に変換したうえで最大HPを素の値(18)に戻す
    private static void MorpheUlt(CharacterState actor)
    {
        if (!actor.isSuspendedAnimation) return; // 安全策(通常はここに到達しない)

        actor.isSuspendedAnimation = false;
        actor.hasRevived = true; // 以後は「生き返った」見た目に切り替える

        var stun = actor.activeEffects.Find(e => e.effectName == "行動不能");
        if (stun != null) actor.activeEffects.Remove(stun);

        actor.currentAttack += actor.morpheMaxHpBonusTotal;
        actor.morpheMaxHpBonusTotal = 0;
        actor.currentMaxHP = actor.data.maxHP; // 最大HPは素の値(18)に戻る
        actor.currentHP = Mathf.Min(actor.currentHP, actor.currentMaxHP);

        actor.NotifyChanged();
    }
}