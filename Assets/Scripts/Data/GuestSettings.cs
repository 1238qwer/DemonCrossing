using UnityEngine;

namespace Mawang
{
    // 용사 손님 확률·변수 조정용 에셋 (Assets/Resources/GuestSettings.asset).
    // 메뉴 Mawang/Guest Settings 로 열고 인스펙터에서 고친다. 플레이 중에 고쳐도 바로 반영된다.
    //
    // 롤러코스터 타이쿤 방식:
    //   · 볼거리(방문할 수 있는 건물)가 열린 공간에 비해 많을수록 손님이 많이 온다.
    //   · 손님은 소지금·체력·기분을 가지고 한 곳씩 골라 들른다.
    //   · 들른 곳에서 줄이 길거나, 돈이 없거나, 관심이 없으면 그냥 지나친다.
    //   · 지치거나(체력 0) 기분이 나쁘거나, 더 둘러볼 마음이 없거나, 갈 곳이 없으면 집에 간다.
    [CreateAssetMenu(fileName = "GuestSettings", menuName = "Mawang/Guest Settings")]
    public class GuestSettings : ScriptableObject
    {
        [Header("손님 수 — 최대치는 주로 [연구] 손님 유치로 늘고, 건물(볼거리)은 최대치에 얼마나 채울지를 정한다 (층 수와는 무관)")]
        [Tooltip("최대 손님 기본값")] public float maxBase = 10f;
        [Tooltip("방문 가능한 칸 1칸마다 최대 손님 +. 방문 가능 칸 = 재고 있는 가게 1칸 · 괴물 있는 전시 우리는 묶어 지은 칸 전체 · 출전 괴물 있는 투기장 1칸")]
        public float maxPerVisitableCell = 0.5f;
        [Tooltip("[연구] 손님 유치 1단계마다 최대 손님 +")] public int maxPerResearchLevel = 10;
        [Tooltip("최대 손님 상한 (화면이 너무 붐비거나 느려지지 않게)")] public int maxCap = 150;
        [Tooltip("볼거리 비율 = 방문 가능한 칸 수 / 열린 칸 수.\n목표 손님 = 최대 손님 × (볼거리 비율 × 이 값)을 아래 하한·상한 사이로. 클수록 성이 비어 있어도 손님이 많다.")]
        public float appealMultiplier = 2.5f;
        [Tooltip("목표 손님 하한 (최대 손님 대비)")] [Range(0f, 1f)] public float minCrowd = 0.5f;
        [Tooltip("목표 손님 상한 (최대 손님 대비)")] [Range(0f, 1f)] public float maxCrowd = 1f;
        [Tooltip("붐빔 물결: 목표 손님을 0 ~ 이 비율만큼 더 올렸다 내렸다 한다 (0.2 = 최대 +20%)")] [Range(0f, 1f)] public float crowdWave = 0.2f;
        [Tooltip("붐빔 물결이 한 번 오르내리는 대략적인 주기(초)")] public float crowdWavePeriod = 90f;
        [Tooltip("빈자리를 채우는 데 걸리는 대략적인 시간(초). 작을수록 빨리 찬다.\n초당 입장 = (목표 − 현재) / 이 값 × 손님 유치 배율")]
        public float fillSeconds = 7f;
        [Tooltip("입장 최소 간격(초)")] public float minSpawnInterval = 0.15f;

        [Header("소지금")]
        [Tooltip("소지금 최소 (지갑 연구·층 배율 전). 상품 가격과 같은 비율로 맞춘다")] public int walletMin = 30;
        [Tooltip("소지금 최대 (지갑 연구·층 배율 전). 상품 가격과 같은 비율로 맞춘다")] public int walletMax = 110;
        [Tooltip("열린 층이 하나 늘 때마다 소지금 배율 (2층 기준). 비싼 상품(최고 약 1,900G)을 살 수 있게 조금씩 커진다")] public float walletPerFloor = 1.1f;

        [Header("체력 · 기분 — 지치거나 기분이 나쁘면 집에 간다")]
        [Tooltip("입장할 때 체력 최소")] public float energyMin = 60f;
        [Tooltip("입장할 때 체력 최대")] public float energyMax = 100f;
        [Tooltip("한 곳 들를 때마다 체력 −")] public float energyPerStop = 25f;
        [Tooltip("입장할 때 기분 최소 (0~100)")] public float happyMin = 60f;
        [Tooltip("입장할 때 기분 최대 (0~100)")] public float happyMax = 90f;
        [Tooltip("기분이 이 값 아래로 떨어지면 집에 간다")] public float leaveBelowHappy = 30f;
        [Tooltip("상품을 샀을 때 기분 +")] public float happyBuy = 10f;
        [Tooltip("전시 우리를 구경했을 때 기분 +")] public float happyWatch = 5f;
        [Tooltip("투기장 대련을 했을 때 기분 +")] public float happySpar = 8f;
        [Tooltip("돈이 모자라거나 재고가 없어 못 샀을 때 기분 −")] public float happyNoMoney = 10f;
        [Tooltip("가게 줄이 꽉 차서 지나칠 때 기분 −")] public float happyQueueFull = 15f;
        [Tooltip("가게에서 기다리다 지쳐 떠날 때 기분 −")] public float happyWaitTimeout = 25f;
        [Tooltip("투기장에 출전 괴물이 없을 때 기분 −")] public float happyNoFighter = 10f;

        [Header("방문지 고르기 — 가중치 (이미 들른 건물은 다시 고르지 않는다)")]
        [Tooltip("식당 가중치")] public float weightRestaurant = 1.5f;
        [Tooltip("기념품점 가중치")] public float weightSouvenir = 1.5f;
        [Tooltip("전시 우리 기본 가중치 (괴물이 있어야 고른다)")] public float weightTankBase = 1f;
        [Tooltip("전시 우리: 분당 관람료 1당 가중치 +")] public float weightTankPerAdmission = 0.05f;
        [Tooltip("전시 우리 가중치 상한 (후반에 관람료가 커져도 가게·투기장에 손님이 가도록)")] public float weightTankMax = 3f;
        [Tooltip("투기장 가중치 (출전 괴물이 있어야 고른다)")] public float weightArena = 3f;
        [Tooltip("손님마다 식욕(0~1)을 굴려 식당은 ×(1 − 이 값 + 식욕×2×이 값), 기념품점은 반대로 곱한다.\n0 = 모든 손님이 같다, 0.5 = 취향 차이가 크다")]
        [Range(0f, 1f)] public float tasteSpread = 0.5f;

        [Header("식당 · 기념품점")]
        [Tooltip("가게 하나에 응대를 기다릴 수 있는 손님 수. 꽉 차면 새 손님은 지나친다 (여러 개 지을 이유)")]
        public int shopQueueLimit = 5;
        [Tooltip("관심 없음: 들어왔지만 아무것도 안 사고 지나칠 기본 확률")] [Range(0f, 1f)] public float notInterestedBase = 0.45f;
        [Tooltip("가게 레벨 1당 '관심 없음' 확률 − (가게가 좋을수록 잘 산다)")] [Range(0f, 0.2f)] public float notInterestedPerLevel = 0.02f;
        [Tooltip("'관심 없음' 확률 하한")] [Range(0f, 1f)] public float notInterestedMin = 0.1f;
        [Tooltip("응대를 기다리는 시간(초) (친절 교육 연구 전)")] public float patience = 12f;

        [Header("전시 우리")]
        [Tooltip("구경 시간 최소(초)")] public float watchMin = 2f;
        [Tooltip("구경 시간 최대(초)")] public float watchMax = 4f;
        [Tooltip("구경 후 추가 관람료를 낼 확률")] [Range(0f, 1f)] public float tipChance = 0.10f;
        [Tooltip("추가 관람료: 그 우리 분당 관람료의 최소 비율")] public float tipMin = 0.10f;
        [Tooltip("추가 관람료: 그 우리 분당 관람료의 최대 비율")] public float tipMax = 0.30f;

        [Header("투기장")]
        [Tooltip("출전 괴물이 있을 때 대련을 신청할 확률 (아니면 구경만 하고 지나친다)")] [Range(0f, 1f)] public float sparChance = 0.6f;
        [Tooltip("대련 연출 시간(초)")] public float boutTime = 2.5f;

        [Header("더 둘러볼지 — 한 곳을 마칠 때마다")]
        [Tooltip("계속 둘러볼 기본 확률")] [Range(0f, 1f)] public float continueBase = 0.35f;
        [Tooltip("돈이 두둑할 때 더해지는 최대 확률. 남은 돈 / (지금 팔리는 가장 비싼 상품 × 아래 배수) 비율만큼")]
        [Range(0f, 1f)] public float continueMoneyBonus = 0.40f;
        [Tooltip("'돈이 두둑하다'의 기준: 가장 비싼 상품 가격의 몇 배")] public float moneyRefMultiple = 3f;
        [Tooltip("들른 곳 하나마다 계속 둘러볼 확률에 곱한다 (작을수록 금방 돌아간다)")] [Range(0f, 1f)] public float continueDecay = 0.85f;
        [Tooltip("한 손님이 들르는 최대 장소 수")] public int maxStops = 8;

        // ── 런타임 접근 ────────────────────────────────────────
        static GuestSettings instance;
        static bool loaded;

        public static GuestSettings I
        {
            get
            {
                if (!loaded)
                {
                    loaded = true;
                    instance = Resources.Load<GuestSettings>("GuestSettings");
                    if (instance == null) instance = CreateInstance<GuestSettings>(); // 에셋이 없으면 기본값
                }
                return instance;
            }
        }
    }
}
