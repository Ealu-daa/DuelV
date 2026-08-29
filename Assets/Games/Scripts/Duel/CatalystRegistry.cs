using System;
using System.Collections.Generic;
using UnityEngine;

public static class CatalystRegistry
{
    private class ActionInfo
    {
        public ActionUI.TargetScope scope;
        public Action<CharacterState, CharacterState> execute;
    }

    private static readonly Dictionary<CatalystId, ActionInfo> catalystTable = new()
    {
        {
            CatalystId.Konshin,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                execute = (actor, target) => KonshinCatalyst(actor)
            }
        },
        {
            CatalystId.Kensyu,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                execute = (actor, target) => KensyuCatalyst(actor)
            }
        },
        {
            CatalystId.Yokusei,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.EnemyForward,
                execute = (actor, target) => YokuseiCatalyst(target)
            }
        },
        {

            CatalystId.Chiyu,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.AllyAny,
                execute = (actor, target) => ChiyuCatalyst(target)
            }
        },
        {
            CatalystId.Jouyu,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                execute = (actor, target) => JouyuCatalyst(actor)
            }
        },
        {
            CatalystId.Kyobou,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                execute = (actor, target) => KyobouCatalyst(actor)
            }
        },
        {
            CatalystId.Fukutsu,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                execute = (actor, target) => FukutsuCatalyst(actor)
            }
        },
        {
            CatalystId.Iatu,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                execute = (actor, target) => IatuCatalyst(actor)
            }
        },
        {
            CatalystId.Kousoku,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.EnemyForward,
                execute = (actor, target) => KousokuCatalyst(target)
            }
        },
        {
            CatalystId.Suijyaku,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.EnemyForward,
                execute = (actor, target) => SuijyakuCatalyst(target)
            }
        },
        {
            CatalystId.Sabaku,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.EnemyForward,
                execute = (actor, target) => SabakuCatalyst(target)
            }
        },
        {
            CatalystId.Kobu,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.AllyForward,
                execute = (actor, target) => KobuCatalyst(target)
            }
        },
        {
            CatalystId.Kihun,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.AllyForward,
                execute = (actor, target) => KihunCatalyst(target)
            }
        },
        {
            CatalystId.Jouka,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                execute = (actor, target) => JoukaCatalyst(actor)
            }
        },
        {
            CatalystId.Fuuin,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.EnemyForward,
                execute = (actor, target) => FuuinCatalyst(target)
            }
        },
        {
            CatalystId.Saisei,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                execute = (actor, target) => SaiseiCatalyst(actor)
            }
        },
        {
            CatalystId.Zanshoku,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.EnemyForward,
                execute = (actor, target) => ZanshokuCatalyst(target)
            }
        },
        {
            CatalystId.Hangeki,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                execute = (actor, target) => HangekiCatalyst(actor)
            }
        },
        {
            CatalystId.Kantuu,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                execute = (actor, target) => KantuuCatalyst(actor)
            }
        },
        {
            CatalystId.Ishi,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                execute = (actor, target) => IshiCatalyst(actor)
            }
        },
        {
            CatalystId.Tenshin,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                execute = (actor, target) => TenshinCatalyst(actor, target)
            }
        },
        {
            CatalystId.Mohou,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                execute = (actor, target) => MohouCatalyst(actor, target)
            }
        },
        {
            CatalystId.Kishuu,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                execute = (actor, target) => KishuuCatalyst(actor, target)
            }
        },
        {
            CatalystId.Tanren,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                execute = (actor, target) => TanrenCatalyst(actor)
            }
        },
        {
            CatalystId.Kenshin,
            new ActionInfo
            {
                scope = ActionUI.TargetScope.Self,
                execute = (actor, target) => KenshinCatalyst(actor)
            }
        },
    };

    // --- カタリスト呼び出し ---
    public static void ExecuteCatalyst(CharacterState actor, CharacterState target, CatalystInstance instance)
    {

        if (instance.isUsed)
        {
            Debug.LogWarning($"使用済みカタリスト: {instance.Id}");
            return;
        }

        if (catalystTable.TryGetValue(instance.Id, out var info))
        {
            info.execute(actor, target);
            instance.MarkUsed();
            actor.hasUsedCatalystThisHalfTurn = true;

            // 使用者のパネル上でカタリストのアイコンをフェード表示する(演出のみ、効果適用は上で完了済み)
            TurnManager.Instance.PlayCatalystFadeEffect(actor, instance.data.icon);

            // 模倣用: 自分のチームが最後に使ったカタリストとして記録する
            TurnManager.Instance.GetTeamOf(actor).lastUsedCatalystId = instance.Id;
        }
        else
        {
            Debug.LogWarning($"未実装のカタリスト: {instance.Id}");
        }
    }

    public static ActionUI.TargetScope GetCatalystTargetScope(CatalystId id)
    {
        if (catalystTable.TryGetValue(id, out var info)) return info.scope;
        Debug.LogWarning($"未設定のTargetScope(catalyst): {id}");
        return ActionUI.TargetScope.Self;
    }

    // --- 中身の実装 ---

    private static void KonshinCatalyst(CharacterState actor)
    {
        int expireAt = TurnManager.Instance.currentHalfTurn + 1;

        actor.currentAttack += 3;

        actor.AddEffect(new StatusEffect
        {
            effectName = "渾身",
            expiresAtHalfTurn = expireAt,
            value = 3
        });
    }
    private static void KensyuCatalyst(CharacterState actor)
    {
        actor.AddEffect(new StatusEffect
        {
            effectName = "堅守",
            expiresAtHalfTurn = int.MaxValue, // 時間経過では消えない、被弾消費のみ
            value = 5
        });
    }
    private static void YokuseiCatalyst(CharacterState target)
    {
        target.currentUltGauge = Mathf.Max(0, target.currentUltGauge - 3);
    }
    private static void ChiyuCatalyst(CharacterState target)
    {
        target.Heal(8);
    }
    private static void JouyuCatalyst(CharacterState actor)
    {
        actor.activeEffects.RemoveAll(e => e.isDebuff);
        actor.Heal(5);
    }

    // 持続1ht攻撃力+7。この間の通常攻撃のたびに味方1体へ7ダメージ(反動処理はTurnManager側)
    private static void KyobouCatalyst(CharacterState actor)
    {
        int expireAt = TurnManager.Instance.currentHalfTurn + 1;

        actor.currentAttack += 7;

        actor.AddEffect(new StatusEffect
        {
            effectName = "狂暴",
            expiresAtHalfTurn = expireAt,
            value = 7
        });
    }

    // 本来死ぬダメージを受けてもHP1で生き残る(1回限り、CharacterState.TakeDamage側で消費)。
    // 発動前に交代すると消える(CharacterState.ClearSwapResetEffects)
    private static void FukutsuCatalyst(CharacterState actor)
    {
        actor.AddEffect(new StatusEffect
        {
            effectName = "不屈",
            expiresAtHalfTurn = int.MaxValue,
        });
    }

    // 持続2ht、相手全員(FW+BK)の交代を封じる
    private static void IatuCatalyst(CharacterState actor)
    {
        var enemyTeam = TurnManager.Instance.GetTeamOf(actor) == BattleManager.Instance.PlayerTeam
            ? BattleManager.Instance.EnemyTeam
            : BattleManager.Instance.PlayerTeam;
        int expireAt = TurnManager.Instance.currentHalfTurn + 2;

        foreach (var member in enemyTeam.AllCharacters())
        {
            if (!member.IsTargetableBy(actor)) continue; // 他人のハンティンググラウンド対象には及ばない
            member.AddEffect(new StatusEffect
            {
                effectName = "交代封じ",
                expiresAtHalfTurn = expireAt,
                isDebuff = true
            });
        }
    }

    // 持続2ht相手FW1体を行動不可にする(ホロウのウルトと同じ効果を流用)
    private static void KousokuCatalyst(CharacterState target)
    {
        int expireAt = TurnManager.Instance.currentHalfTurn + 2;

        target.AddEffect(new StatusEffect
        {
            effectName = "行動不能",
            expiresAtHalfTurn = expireAt,
            isDebuff = true
        });
    }

    // 持続2ht相手FW1体の攻撃力を半減(切り上げ)。期限が来たら元の値に戻す
    private static void SuijyakuCatalyst(CharacterState target)
    {
        int expireAt = TurnManager.Instance.currentHalfTurn + 2;
        int reduction = target.currentAttack - Mathf.CeilToInt(target.currentAttack / 2f);

        target.currentAttack -= reduction;

        target.AddEffect(new StatusEffect
        {
            effectName = "衰弱",
            expiresAtHalfTurn = expireAt,
            value = reduction,
            isDebuff = true
        });
    }

    // 相手FW1体の現在バフを剥がし、持続2ht新規バフを無効化する(CharacterState.AddEffect側で鎖縛を見て弾く)
    private static void SabakuCatalyst(CharacterState target)
    {
        target.activeEffects.RemoveAll(e => !e.isDebuff);

        int expireAt = TurnManager.Instance.currentHalfTurn + 2;
        target.AddEffect(new StatusEffect
        {
            effectName = "鎖縛",
            expiresAtHalfTurn = expireAt,
            isDebuff = true
        });
    }

    // 味方FW1体のULTゲージ+2
    private static void KobuCatalyst(CharacterState target)
    {
        target.AddUltGauge(2);
    }

    // 持続1ht味方FW1体の攻撃力+2、防御力+2
    private static void KihunCatalyst(CharacterState target)
    {
        int expireAt = TurnManager.Instance.currentHalfTurn + 1;

        target.currentAttack += 2;
        target.currentDefense += 2;

        target.AddEffect(new StatusEffect
        {
            effectName = "奮起",
            expiresAtHalfTurn = expireAt
        });
    }

    // 味方全体のデバフを一括解除
    private static void JoukaCatalyst(CharacterState actor)
    {
        var team = TurnManager.Instance.GetTeamOf(actor);
        foreach (var member in team.AllCharacters())
        {
            if (!member.IsTargetableBy(actor)) continue; // 他人のハンティンググラウンド対象には及ばない
            member.activeEffects.RemoveAll(e => e.isDebuff);
        }
    }

    // 相手FW1体のスキル・ULT両方を持続2ht封じる(サイレンス+ULT封じを同時付与)
    private static void FuuinCatalyst(CharacterState target)
    {
        int expireAt = TurnManager.Instance.currentHalfTurn + 2;

        target.AddEffect(new StatusEffect
        {
            effectName = "サイレンス",
            expiresAtHalfTurn = expireAt,
            isDebuff = true
        });
        target.AddEffect(new StatusEffect
        {
            effectName = "ULT封じ",
            expiresAtHalfTurn = expireAt,
            isDebuff = true
        });
    }

    // 味方FW2体に2回復×3ht(HoT)を付与
    private static void SaiseiCatalyst(CharacterState actor)
    {
        var team = TurnManager.Instance.GetTeamOf(actor);
        int expireAt = TurnManager.Instance.currentHalfTurn + 3;

        foreach (var fw in team.forwards)
        {
            if (!fw.IsTargetableBy(actor)) continue; // 他人のハンティンググラウンド対象には及ばない
            fw.AddEffect(new StatusEffect
            {
                effectName = "再生",
                expiresAtHalfTurn = expireAt,
                tickValue = 2
            });
        }
    }

    // 相手FW1体に2ダメ×4ht(DoT)を付与
    private static void ZanshokuCatalyst(CharacterState target)
    {
        int expireAt = TurnManager.Instance.currentHalfTurn + 4;

        target.AddEffect(new StatusEffect
        {
            effectName = "残蝕",
            expiresAtHalfTurn = expireAt,
            tickValue = -2,
            isDebuff = true
        });
    }

    // 持続2ht、被弾したら自分の攻撃力分を攻撃者に反撃する(CharacterState.TakeDamage側で処理)
    private static void HangekiCatalyst(CharacterState actor)
    {
        int expireAt = TurnManager.Instance.currentHalfTurn + 2;

        actor.AddEffect(new StatusEffect
        {
            effectName = "反撃",
            expiresAtHalfTurn = expireAt
        });
    }

    // 持続1ht、敵を倒した時の超過ダメージをもう一方のFWへ与える(CharacterState.TakeDamage側で処理)
    private static void KantuuCatalyst(CharacterState actor)
    {
        int expireAt = TurnManager.Instance.currentHalfTurn + 1;

        actor.AddEffect(new StatusEffect
        {
            effectName = "貫通",
            expiresAtHalfTurn = expireAt
        });
    }

    // 戦闘不能時、防御力分のダメージを倒した相手に与える(CharacterState.TakeDamage側で処理)。
    // 発動前に交代すると消える(CharacterState.ClearSwapResetEffects)
    private static void IshiCatalyst(CharacterState actor)
    {
        actor.AddEffect(new StatusEffect
        {
            effectName = "遺志",
            expiresAtHalfTurn = int.MaxValue
        });
    }

    // 行動消費なしで交代する(ForceSwapは交代してきた側もこのターン行動可能)
    // ActionUIで選んだ場合はtargetにその交代先が入っている。無ければランダム(現状カタリストは
    // プレイヤーしか使わないため、このフォールバックは基本通らない安全策)
    private static void TenshinCatalyst(CharacterState actor, CharacterState target)
    {
        var team = TurnManager.Instance.GetTeamOf(actor);
        var incoming = (target != null && !target.IsDefeated && team.backups.Contains(target)) ? target : null;

        if (incoming == null)
        {
            var aliveBackups = team.backups.FindAll(c => !c.IsDefeated);
            if (aliveBackups.Count == 0) return;
            incoming = aliveBackups[UnityEngine.Random.Range(0, aliveBackups.Count)];
        }

        TurnManager.Instance.ForceSwap(actor, incoming);
    }

    // 敵が最後に使ったカタリストのうち、ロールが一致してコピー可能なものを判定する(ActionUI側で
    // 対象選択のスコープを知るために使う)。無ければnull
    public static CatalystId? GetMohouMimicCandidate(CharacterState actor)
    {
        var myTeam = TurnManager.Instance.GetTeamOf(actor);
        var enemyTeam = myTeam == BattleManager.Instance.PlayerTeam
            ? BattleManager.Instance.EnemyTeam
            : BattleManager.Instance.PlayerTeam;

        var lastId = enemyTeam.lastUsedCatalystId;
        if (lastId == null || lastId.Value == CatalystId.Mohou) return null; // 未使用、または模倣自体は無限再帰になるので対象外

        var data = CatalystDataRegistry.Instance != null ? CatalystDataRegistry.Instance.GetData(lastId.Value) : null;
        if (data == null) return null;

        // ロールが一致しなければ発動しない(Role.Noneの"オール"カタリストは誰でもコピー可)
        Role casterRole = (Role)actor.data.RoleGroup;
        if (data.restrictedRole != Role.None && data.restrictedRole != casterRole) return null;

        if (!catalystTable.ContainsKey(lastId.Value)) return null;

        return lastId.Value;
    }

    // 敵が最後に使ったカタリストを、ロールが一致すればコピーして使用する。
    // ActionUIの対象選択で選んだ場合はtargetにその対象が入っている。無ければ(基本通らない
    // 安全策として)コピー元のTargetScopeに応じてランダムに選ぶ
    private static void MohouCatalyst(CharacterState actor, CharacterState target)
    {
        var mimicId = GetMohouMimicCandidate(actor);
        if (mimicId == null) return;
        if (!catalystTable.TryGetValue(mimicId.Value, out var info)) return;

        var mimicTarget = target;
        if (mimicTarget == null)
        {
            var myTeam = TurnManager.Instance.GetTeamOf(actor);
            var enemyTeam = myTeam == BattleManager.Instance.PlayerTeam
                ? BattleManager.Instance.EnemyTeam
                : BattleManager.Instance.PlayerTeam;

            mimicTarget = info.scope switch
            {
                ActionUI.TargetScope.EnemyForward => PickRandomAlive(enemyTeam.forwards),
                ActionUI.TargetScope.AllyForward => PickRandomAlive(myTeam.forwards),
                ActionUI.TargetScope.EnemyAll => PickRandomAlive(new List<CharacterState>(enemyTeam.AllCharacters())),
                ActionUI.TargetScope.AllyAny => PickRandomAlive(new List<CharacterState>(myTeam.AllCharacters())),
                _ => actor
            };
        }
        if (mimicTarget == null) return;

        info.execute(actor, mimicTarget);
    }

    private static CharacterState PickRandomAlive(List<CharacterState> candidates)
    {
        var alive = candidates.FindAll(c => !c.IsDefeated);
        if (alive.Count == 0) return null;
        return alive[UnityEngine.Random.Range(0, alive.Count)];
    }

    // 自分自身が攻撃力-3で相手BKを奇襲攻撃し、相手FW(生きていればランダムに1体)から
    // その攻撃力分の反撃を食らう。ActionUIで選んだ場合はtargetに狙う相手BKが入っている。
    // 無ければ(現状カタリストはプレイヤーのみ使用のため基本通らない安全策として)ランダム
    private static void KishuuCatalyst(CharacterState actor, CharacterState target)
    {
        var team = TurnManager.Instance.GetTeamOf(actor);
        var enemyTeam = team == BattleManager.Instance.PlayerTeam
            ? BattleManager.Instance.EnemyTeam
            : BattleManager.Instance.PlayerTeam;

        var actualTarget = (target != null && !target.IsDefeated && enemyTeam.backups.Contains(target)) ? target : null;
        if (actualTarget == null)
        {
            var aliveBackups = enemyTeam.backups.FindAll(c => !c.IsDefeated);
            if (aliveBackups.Count == 0) return;
            actualTarget = aliveBackups[UnityEngine.Random.Range(0, aliveBackups.Count)];
        }

        int damage = Mathf.Max(0, actor.currentAttack - 3);
        actualTarget.TakeDamage(damage, actor);

        // 相手FW(生きていれば1体ランダム)から反撃を受ける
        var aliveEnemyFw = enemyTeam.forwards.FindAll(c => !c.IsDefeated);
        if (aliveEnemyFw.Count > 0)
        {
            var counterFw = aliveEnemyFw[UnityEngine.Random.Range(0, aliveEnemyFw.Count)];
            actor.TakeDamage(counterFw.currentAttack, counterFw, isReactionDamage: true);
        }
    }

    // 最大HP+5(回復なし)、攻撃力+1、防御力+1。永続で重ねがけ可能
    private static void TanrenCatalyst(CharacterState actor)
    {
        actor.currentMaxHP += 5;
        actor.currentAttack += 1;
        actor.currentDefense += 1;
        actor.NotifyChanged();
    }

    // 持続2ht、味方FWが受けるダメージを肩代わりする(フォートのインターセプトと同じ仕組み)。
    // その間、自分は防御行動しか選べなくなる(ActionUI側でIsDefenseOnlyを見てガード)
    private static void KenshinCatalyst(CharacterState actor)
    {
        var team = TurnManager.Instance.GetTeamOf(actor);
        var ally = team.forwards.Find(c => c != actor);
        if (ally == null) return;

        int expireAt = TurnManager.Instance.currentHalfTurn + 2;

        ally.protectedBy = actor;
        actor.isProtectingAlly = true;

        actor.AddEffect(new StatusEffect
        {
            effectName = "献身",
            expiresAtHalfTurn = expireAt
        });
    }
}