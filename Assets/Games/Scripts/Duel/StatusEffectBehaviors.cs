/// <summary>
/// StatusEffectが期限切れになった時の処理を、effectName(文字列)から解決する。
///
/// もともとは各効果の生成箇所(SkillRegistry/CatalystRegistry)でStatusEffect.onExpireにラムダを
/// 直接持たせていたが、それだとオンライン対戦の再接続用スナップショット(Firestoreへの状態保存/復元)が
/// 作れない(デリゲートはJSONに保存できない)ため、名前ベースのディスパッチに一本化した。
///
/// 効果を新規追加する時は、ここにもケースを足すこと(期限切れ時に何かする効果の場合のみ)。
/// </summary>
public static class StatusEffectBehaviors
{
    public static void InvokeOnExpire(StatusEffect effect, CharacterState owner)
    {
        switch (effect.effectName)
        {
            // --- 単純な数値バフ/デバフの巻き戻し(付与時にvalueへ変動量を入れておく) ---
            case "エンプティフィル":     // スキル: 攻撃力+5
            case "渾身":                // カタリスト: 攻撃力+3
            case "狂暴":                // カタリスト: 攻撃力+7
                owner.currentAttack -= effect.value;
                break;

            case "衰弱スタック":         // スキル: 攻撃力-1(スタック式)
            case "衰弱":                // カタリスト: 攻撃力-reduction
            case "狩猟印(攻撃力減少)":    // スキル: 攻撃力-2
                owner.currentAttack += effect.value;
                break;

            case "防御力低下":           // スキル: 防御力を0にする(defReduction分を戻す)
                owner.currentDefense += effect.value;
                break;

            case "奮起":                // カタリスト: 攻撃力+2、防御力+2(固定値、valueは使っていない)
                owner.currentAttack -= 2;
                owner.currentDefense -= 2;
                break;

            // --- 被弾肩代わり関係の解除(インターセプト/献身で共通の後始末) ---
            case "インターセプト":       // スキル
            case "献身":                // カタリスト
                {
                    owner.isProtectingAlly = false;
                    var team = TurnManager.Instance.GetTeamOf(owner);
                    var protectedAlly = team.forwards.Find(x => x.protectedBy == owner);
                    if (protectedAlly != null) protectedAlly.protectedBy = null;
                }
                break;

            // --- 特定の相手との紐付け解除(sourceに紐付け相手を入れておく) ---
            case "ツインバーデン":
                if (effect.source != null && owner.twinBurdenPartner == effect.source)
                    owner.twinBurdenPartner = null;
                break;

            case "獲物指定":
                if (effect.source != null && owner.huntBoundTo == effect.source)
                    owner.huntBoundTo = null;
                break;

            // それ以外(サイレンス・行動不能・交代封じ・鎖縛・堅守・不屈・遺志・反撃・貫通など)は
            // 期限切れ時に何もしない(効果自体がactiveEffectsから消えるだけで完結する)ので何もしない
            default:
                break;
        }
    }
}
