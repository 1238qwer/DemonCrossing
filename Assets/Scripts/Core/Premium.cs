using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if !ONESTORE
using UnityEngine.Purchasing;
#endif

namespace Mawang
{
    // 프리미엄 버전 (인앱 결제 1회, 비소모성): 게임 배속 2·3배를 연다.
    // 구매 여부는 세이브가 아니라 PlayerPrefs 에 둔다 — '새로 시작'을 해도 남는다. 재설치하면 스토어 구매 내역으로 복원한다.
    // 스토어: 기본은 Google Play (Unity IAP). 원스토어 빌드(ONESTORE 심볼, Mawang/Store/Build ONE store AAB)는 Premium.OneStore.cs.
    public static partial class Premium
    {
        public const string ProductId = "demoncrossingpremiumver";
        public const int MaxSpeed = 3;
        const string OwnedKey = "premium_owned", SpeedKey = "game_speed";

        public static bool Owned { get; private set; }
        public static bool StoreReady { get; private set; }
        public static bool Purchasing { get; private set; }
        // 스토어가 알려 준 현지 통화 가격 (예: ₩1,100 · $0.99). 상품 조회 전에는 null.
        public static string Price { get; private set; }
        public static event Action Changed;

        public static int Speed { get; private set; } = 1;

        // 원스토어 PC(윈도우 exe)는 결제 SDK 가 없다 → 일단 배속을 무료로 연다. 나중에 유료로 바꾸려면 false.
        // 스팀(STEAM)은 따로 정한다.
#if UNITY_STANDALONE && !UNITY_EDITOR && !STEAM
        static readonly bool FreeOnThisPlatform = true;
#else
        static readonly bool FreeOnThisPlatform = false;
#endif

        public static void Init()
        {
            if (FreeOnThisPlatform)
            {
                Owned = true; // PlayerPrefs 에는 남기지 않는다 — 유료로 바꾸면 바로 잠긴다
                ApplySpeed(PlayerPrefs.GetInt(SpeedKey, 1));
                return;
            }
            Owned = PlayerPrefs.GetInt(OwnedKey, 0) == 1;
            ApplySpeed(Owned ? PlayerPrefs.GetInt(SpeedKey, 1) : 1);
            Connect();
        }

        // 1 → 2 → 3 → 1 (프리미엄만)
        public static void CycleSpeed()
        {
            if (!Owned) return;
            ApplySpeed(Speed >= MaxSpeed ? 1 : Speed + 1);
            PlayerPrefs.SetInt(SpeedKey, Speed);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        static void ApplySpeed(int s)
        {
            Speed = Mathf.Clamp(s, 1, MaxSpeed);
            Time.timeScale = Speed;
        }

#if !ONESTORE
        static StoreController store;

        static async void Connect()
        {
            if (store != null) return; // 언어를 바꾸면 장면을 다시 불러온다 — 연결은 한 번만
            try
            {
                store = UnityIAPServices.StoreController();
                store.OnPurchasePending += OnPurchasePending;
                store.OnPurchaseConfirmed += OnPurchaseConfirmed;
                store.OnPurchaseFailed += OnPurchaseFailed;
                store.OnPurchasesFetched += OnPurchasesFetched;
                store.OnProductsFetched += products =>
                {
                    StoreReady = true;
                    var p = products.FirstOrDefault(x => x.definition.id == ProductId);
                    if (p != null && !string.IsNullOrEmpty(p.metadata.localizedPriceString)) Price = p.metadata.localizedPriceString;
                    store.FetchPurchases();
                    Changed?.Invoke();
                };
                store.OnProductsFetchFailed += f => Debug.LogWarning($"[IAP] 상품 조회 실패: {f.FailureReason}");
                await store.Connect();
                store.FetchProducts(new List<ProductDefinition> { new ProductDefinition(ProductId, ProductType.NonConsumable) });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[IAP] 스토어 연결 실패: {e.Message}");
            }
        }

        public static void Buy()
        {
            if (Owned) return;
            var product = store?.GetProductById(ProductId);
            if (!StoreReady || product == null)
            {
                StoreUnavailable();
                return;
            }
            Purchasing = true;
            Changed?.Invoke();
            store.PurchaseProduct(product);
        }

        public static void Restore()
        {
            if (store == null || !StoreReady)
            {
                StoreUnavailable();
                return;
            }
            store.RestoreTransactions((ok, err) =>
            {
                store.FetchPurchases();
                if (!ok) Game.I.Fail(L.T("구매 복원에 실패했습니다.", "Restore failed.") + (string.IsNullOrEmpty(err) ? "" : $"\n{err}"));
            });
        }

        static bool HasOurProduct(Order order) => order.CartOrdered.Items().Any(i => i.Product.definition.id == ProductId);

        // 혜택을 먼저 주고 구매를 확정한다
        static void OnPurchasePending(PendingOrder order)
        {
            if (HasOurProduct(order)) Grant(true);
            store.ConfirmPurchase(order);
        }

        static void OnPurchaseConfirmed(Order order)
        {
            if (order is FailedOrder failed) { OnPurchaseFailed(failed); return; }
            Purchasing = false;
            if (HasOurProduct(order)) Grant(false);
            Changed?.Invoke();
        }

        static void OnPurchaseFailed(FailedOrder order)
        {
            Purchasing = false;
            Changed?.Invoke();
            if (order.FailureReason == PurchaseFailureReason.UserCancelled) return;
            Debug.LogWarning($"[IAP] 구매 실패: {order.FailureReason} {order.Details}");
            Game.I.Fail(L.T("구매에 실패했습니다.", "Purchase failed."));
        }

        // 스토어의 구매 내역: 재설치·다른 기기에서도 복원된다
        static void OnPurchasesFetched(Orders orders)
        {
            if (orders.ConfirmedOrders.Any(HasOurProduct)) Grant(false);
        }

#endif

        static void StoreUnavailable() => Game.I.Fail(L.T("스토어에 연결할 수 없습니다. 잠시 후 다시 시도하세요.", "Can't reach the store. Please try again later."));

        static void Grant(bool announce)
        {
            if (Owned) return;
            Owned = true;
            PlayerPrefs.SetInt(OwnedKey, 1);
            PlayerPrefs.Save();
            Changed?.Invoke();
            if (announce || Purchasing)
            {
                Sound.Play("upgrade");
                Game.I.Notify(L.T("프리미엄 버전이 적용되었습니다! 배속 버튼으로 최대 3배속까지 쓸 수 있습니다.", "Premium unlocked! Use the speed button for up to 3x."), "ic_star");
            }
        }
    }
}
