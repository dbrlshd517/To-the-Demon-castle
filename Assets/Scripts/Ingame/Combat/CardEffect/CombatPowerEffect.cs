using DeckRoguelike.UI;
using DeckRoguelike.Cards;

namespace DeckRoguelike.Combat
{
    /// <summary>
    /// 파워 카드로 등록되는 전투 지속 효과의 베이스 클래스.
    /// 카드가 플레이될 때 CombatController.RegisterPower()로 등록되고
    /// 전투가 끝나면 자동으로 해제됩니다.
    /// </summary>
    public abstract class CombatPowerEffect
    {
        public int Value { get; protected set; }

        /// <summary>RegisterPower 시 BoardController가 채워주는 UI 아이콘 키. 1회성 파워가 소비될 때 UnregisterPower로 같이 제거된다.</summary>
        public string IconKey { get; internal set; }

        protected CombatPowerEffect(int value) { Value = value; }

        /// <summary>플레이어 턴 시작 시 호출</summary>
        public virtual void OnTurnStart(BoardController board) {}

        /// <summary>플레이어 턴 종료 시 호출 (이번 턴 한정 파워의 만료 처리에 사용)</summary>
        public virtual void OnTurnEnd(BoardController board) {}

        /// <summary>적이 사망할 때마다 호출</summary>
        public virtual void OnEnemyKilled(BoardController board) {}

        /// <summary>플레이어가 HP를 잃을 때마다 호출 (카드 효과 포함)</summary>
        public virtual void OnPlayerLostHp(BoardController board, int amount) {}

        /// <summary>카드가 소멸(Exhaust)될 때마다 호출</summary>
        public virtual void OnCardExhausted(BoardController board) {}

        /// <summary>적이 0보다 큰 피해(방어도 통과 후)를 입을 때마다 호출</summary>
        public virtual void OnEnemyDamaged(BoardController board, EnemyInstance enemy, int amount) {}

        /// <summary>플레이어가 카드를 사용한 직후 호출 (파워 카드 자기 자신 포함)</summary>
        public virtual void OnCardPlayed(BoardController board, CardData card) {}

        /// <summary>플레이어가 적의 공격을 받기 직전(블록 차감 전) 호출. 반사/가시 효과 등에 사용.</summary>
        public virtual void OnPlayerAttacked(BoardController board, EnemyInstance attacker, int rawDamage) {}

        /// <summary>발사 공격(Shoot/Allrange_Shoot/6times_Shoot) 1회마다 호출. shotCount는 전투 누적 발사 횟수.</summary>
        public virtual void OnShoot(BoardController board, int shotCount) {}

        /// <summary>트랩이 설치될 때마다 호출. trapCount는 전투 누적 트랩 설치 횟수.</summary>
        public virtual void OnTrapPlaced(BoardController board, int trapCount) {}
    }

    /// <summary>공격을 당했을 때 공격자에게 Value 피해를 줍니다 (가시갑옷).</summary>
    public class AttackedDamagePower : CombatPowerEffect
    {
        public AttackedDamagePower(int value) : base(value) {}
        public override void OnPlayerAttacked(BoardController c, EnemyInstance attacker, int rawDamage)
        {
            if (attacker == null || attacker.CurrentHP <= 0) return;
            c.DamageEnemy(attacker, Value);
        }
    }

    /// <summary>이번 턴 동안 받는 공격을 공격자에게 그대로 반사합니다 (방어태세).</summary>
    public class ReflectionDamagePower : CombatPowerEffect
    {
        public int RemainingTurns;
        public ReflectionDamagePower(int value, int turns) : base(value)
        {
            RemainingTurns = UnityEngine.Mathf.Max(1, turns);
        }
        public override void OnTurnStart(BoardController c)
        {
            if (RemainingTurns > 0) RemainingTurns--;
        }
        public override void OnPlayerAttacked(BoardController c, EnemyInstance attacker, int rawDamage)
        {
            if (RemainingTurns <= 0) return;
            if (attacker == null || attacker.CurrentHP <= 0) return;
            int reflect = Value > 0 ? Value : rawDamage;
            c.DamageEnemy(attacker, reflect);
        }
    }

    /// <summary>이번 턴에 사용되는 다음 액션카드를 Value장 복사합니다 (1회성, 턴 종료 시 미사용이면 만료).</summary>
    public class NextUseCopyCardPower : CombatPowerEffect
    {
        private bool _consumed;
        public NextUseCopyCardPower(int value) : base(value) {}
        public override void OnCardPlayed(BoardController c, CardData card)
        {
            if (_consumed) return;
            if (card == null || card.CardTypeFromCode != CardType.Action) return;
            // 자기 자신을 등록한 카드(nextuse_copyCard 효과를 가진 카드: 22212/22213 등)에는 발동하지 않는다.
            // 이 가드가 없으면 22213을 연속 사용할 때 이전 NU가 22213 자체를 복사해 손패에 사본이 끝없이 남는다.
            if (card.Effects != null)
            {
                foreach (var fx in card.Effects)
                {
                    if (fx?.customEffectId != null &&
                        fx.customEffectId.Equals("nextuse_copyCard", System.StringComparison.OrdinalIgnoreCase))
                        return;
                }
            }
            _consumed = true;
            for (int i = 0; i < UnityEngine.Mathf.Max(1, Value); i++)
            {
                var copy = card.Clone();
                c.AddCardToHandFree(copy);
            }
            // 1회성 파워: 소비됐으니 활성 파워 목록과 UI 아이콘에서 제거
            c.UnregisterPower(this);
        }

        public override void OnTurnEnd(BoardController c)
        {
            // 이번 턴 안에 액션카드를 쓰지 않았다면 파워 만료 — 다음 턴까지 남지 않는다.
            if (_consumed) return;
            c.UnregisterPower(this);
        }
    }

    // ── 구체 구현 ────────────────────────────────────────────────────────────

    /// <summary>매 턴 시작 시 힘 +Value</summary>
    public class EveryTurnStrengthPower : CombatPowerEffect
    {
        public EveryTurnStrengthPower(int value) : base(value) {}
        public override void OnTurnStart(BoardController c) => c.AddStrength(Value);
    }

    /// <summary>적 처치 시 힘 +Value</summary>
    public class EveryKillStrengthPower : CombatPowerEffect
    {
        public EveryKillStrengthPower(int value) : base(value) {}
        public override void OnEnemyKilled(BoardController c) => c.AddStrength(Value);
    }

    /// <summary>HP를 잃을 때마다 힘 +Value</summary>
    public class EveryLoseHpStrengthPower : CombatPowerEffect
    {
        public EveryLoseHpStrengthPower(int value) : base(value) {}
        public override void OnPlayerLostHp(BoardController c, int amount) => c.AddStrength(Value);
    }

    /// <summary>카드 소멸 시 힘 +Value</summary>
    public class EveryExhaustsStrengthPower : CombatPowerEffect
    {
        public EveryExhaustsStrengthPower(int value) : base(value) {}
        public override void OnCardExhausted(BoardController c) => c.AddStrength(Value);
    }

    /// <summary>
    /// 적이 피해를 입을 때마다 짧은 텀 후 +Value 추가 피해를 줍니다 (소매치기).
    /// 추가 피해는 OnEnemyDamaged 훅을 다시 발동시키지 않아 무한 루프를 방지합니다.
    /// </summary>
    public class DamegedMoreDamagePower : CombatPowerEffect
    {
        public DamegedMoreDamagePower(int value) : base(value) {}
        public override void OnEnemyDamaged(BoardController c, EnemyInstance enemy, int amount)
        {
            if (enemy == null || enemy.CurrentHP <= 0) return;
            c.StartFollowUpDamage(enemy, Value, 0.18f);
        }
    }

    /// <summary>매 턴 시작 시 단검 카드를 한 장 손에 추가합니다 (Value 0=22002, 1=22003).</summary>
    public class EveryTurnCreateKnifePower : CombatPowerEffect
    {
        public EveryTurnCreateKnifePower(int value) : base(value) {}
        public override void OnTurnStart(BoardController c)
        {
            int code = Value >= 1 ? 22003 : 22002;
            var card = DeckRoguelike.Core.CardRegistry.GetCard(code);
            if (card != null) c.AddCardToHandFree(card);
        }
    }

    /// <summary>매 턴 시작 시 채력 1을 잃고 지정 좌표의 적에게 Value 피해 (혈사포).</summary>
    public class EveryLoseHpAllRangeDamagePower : CombatPowerEffect
    {
        private readonly UnityEngine.Vector2Int[] _offsets;

        public EveryLoseHpAllRangeDamagePower(int value, UnityEngine.Vector2Int[] offsets) : base(value)
        {
            _offsets = offsets;
        }

        public override void OnTurnStart(BoardController c)
        {
            if (DeckRoguelike.Core.GameManager.Instance != null)
            {
                DeckRoguelike.Core.GameManager.Instance.Heal(-1);
                c.FirePowerOnPlayerLostHp(1);
            }
            foreach (var off in _offsets)
            {
                var enemy = c.GetEnemyAt(c.PlayerSpawnCell + off);
                if (enemy != null) c.DamageEnemy(enemy, Value);
            }
        }
    }

    /// <summary>매 턴 시작 시 채력 HpLoss를 잃고 카드를 DrawCount장 뽑습니다 (광폭화).</summary>
    public class EveryLoseHpDrawPower : CombatPowerEffect
    {
        public int HpLoss { get; }
        public int DrawCount { get; }
        public EveryLoseHpDrawPower(int hpLoss, int drawCount) : base(drawCount)
        {
            HpLoss = hpLoss;
            DrawCount = drawCount;
        }
        public override void OnTurnStart(BoardController c)
        {
            if (HpLoss > 0 && DeckRoguelike.Core.GameManager.Instance != null)
            {
                DeckRoguelike.Core.GameManager.Instance.Heal(-HpLoss);
                c.FirePowerOnPlayerLostHp(HpLoss);
            }
            if (DrawCount > 0) c.DrawExtraCards(DrawCount);
        }
    }

    /// <summary>소멸 카드를 사용할 때마다 힘 +Value (근력 강화 - 소멸).</summary>
    public class ExhaustsCardStrengthPower : CombatPowerEffect
    {
        public ExhaustsCardStrengthPower(int value) : base(value) {}
        public override void OnCardExhausted(BoardController c) => c.AddStrength(Value);
    }

    /// <summary>소멸 카드를 사용할 때마다 카드 Value장 드로우 (광폭화 - 소멸).</summary>
    public class ExhaustsCardDrawPower : CombatPowerEffect
    {
        public ExhaustsCardDrawPower(int value) : base(value) {}
        public override void OnCardExhausted(BoardController c) => c.DrawExtraCards(Value);
    }

    /// <summary>이동 카드를 사용할 때마다 힘 +Value.</summary>
    public class MoveCardStrengthPower : CombatPowerEffect
    {
        public MoveCardStrengthPower(int value) : base(value) {}
        public override void OnCardPlayed(BoardController c, CardData card)
        {
            if (card != null && card.CardTypeFromCode == CardType.Move) c.AddStrength(Value);
        }
    }

    /// <summary>파워 카드를 사용할 때마다 힘 +Value (자기 자신 카드 포함).</summary>
    public class PowerCardStrengthPower : CombatPowerEffect
    {
        public PowerCardStrengthPower(int value) : base(value) {}
        public override void OnCardPlayed(BoardController c, CardData card)
        {
            if (card != null && card.CardTypeFromCode == CardType.Power) c.AddStrength(Value);
        }
    }

    // ── Shoot 파생 파워들 ────────────────────────────────────────────────

    /// <summary>발사를 Value회 할 때마다 공격 대상이 아닌 무작위 적에게 추가 발사 (도탄 33202/33203).</summary>
    public class EveryShootRicochetPower : CombatPowerEffect
    {
        private readonly int _interval;
        public EveryShootRicochetPower(int interval) : base(interval)
        {
            _interval = UnityEngine.Mathf.Max(1, interval);
        }
        public override void OnShoot(BoardController c, int shotCount)
        {
            if (shotCount <= 0 || shotCount % _interval != 0) return;
            int dmg = 6 + BoardController.CurrentShootDamage + c.PlayerStrength;
            c.FireRicochetShot(dmg);
        }
    }

    /// <summary>발사할 때마다 힘 +Value (무기 숙련 33302/33303).</summary>
    public class EveryShootStrengthPower : CombatPowerEffect
    {
        public EveryShootStrengthPower(int value) : base(value) {}
        public override void OnShoot(BoardController c, int shotCount) => c.AddStrength(Value);
    }

    /// <summary>발사를 Value회 할 때마다 이동 카드 1장을 손에 추가 (33300/33301).</summary>
    public class ShootMoveCardPower : CombatPowerEffect
    {
        private readonly int _interval;
        public ShootMoveCardPower(int interval) : base(interval)
        {
            _interval = UnityEngine.Mathf.Max(1, interval);
        }
        public override void OnShoot(BoardController c, int shotCount)
        {
            if (shotCount <= 0 || shotCount % _interval != 0) return;
            c.AddRandomMoveCardToHand();
        }
    }

    /// <summary>발사할 때마다 적과의 Manhattan 거리만큼 추가 데미지 누적 (저격 33304/33305).
    /// 거리 1당 데미지 ×Value배 (실제로는 발사 데미지 계산식에서 직접 처리).</summary>
    public class ShootSnipePower : CombatPowerEffect
    {
        public int Multiplier { get; }
        public ShootSnipePower(int multiplier) : base(multiplier)
        {
            Multiplier = UnityEngine.Mathf.Max(1, multiplier);
        }
    }

    /// <summary>발사가 도착 칸 + 4방향에 동일 피해를 입힙니다 (유탄발사 33306/33307).</summary>
    public class ShootAreaAttackPower : CombatPowerEffect
    {
        public ShootAreaAttackPower(int value) : base(value) {}
    }

    /// <summary>트랩을 설치할 때마다 힘 +Value (설치가속 33104/33105).</summary>
    public class EveryTrapStrengthPower : CombatPowerEffect
    {
        public EveryTrapStrengthPower(int value) : base(value) {}
        public override void OnTrapPlaced(BoardController c, int trapCount) => c.AddStrength(Value);
    }

    /// <summary>트랩을 설치할 때마다 발사 카드(32000)를 Value장 손에 추가 (33302/33303 변형).</summary>
    public class EveryTrapShootCardPower : CombatPowerEffect
    {
        public EveryTrapShootCardPower(int value) : base(value) {}
        public override void OnTrapPlaced(BoardController c, int trapCount)
        {
            var card = DeckRoguelike.Core.CardRegistry.GetCard(32000);
            if (card == null) return;
            int count = UnityEngine.Mathf.Max(1, Value);
            for (int i = 0; i < count; i++) c.AddCardToHandFree(card);
        }
    }

    /// <summary>한파(43300/43301): Value턴(=주기)에 한 번, 매 플레이어 턴 시작 시 모든 적에게 냉기 1스택을 부여합니다.
    /// Value=1이면 매 턴, Value=2면 2턴마다. 냉기 3 누적 시 해당 적은 빙결로 전환됩니다.
    /// 첫 발동은 카드를 사용한 다음 플레이어 턴 시작부터입니다 (다른 매턴 파워와 동일).</summary>
    public class EveryNTurnsChillPower : CombatPowerEffect
    {
        private readonly int _interval;
        private int _counter;
        public EveryNTurnsChillPower(int interval) : base(interval)
        {
            _interval = UnityEngine.Mathf.Max(1, interval);
        }
        public override void OnTurnStart(BoardController c)
        {
            _counter++;
            if (_counter < _interval) return;
            _counter = 0;
            c.AddColdToAllEnemies(1);
        }
    }
}
