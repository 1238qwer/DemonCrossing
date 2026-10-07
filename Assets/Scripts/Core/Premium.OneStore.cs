#if ONESTORE
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using OneStore.Auth;
using OneStore.Purchasing;
using UnityEngine;
using ResponseCode = OneStore.Purchasing.ResponseCode; // OneStore.Auth 에도 같은 이름이 있다

namespace Mawang
{
    // 원스토어 결제 (원스토어 빌드 전용). 프리미엄은 '관리형 상품'이라 소비하지 않고 구매 확인(acknowledge)만 한다.
    // 원스토어 SDK 는 안드로이드 기기에서만 동작한다 — 에디터에서는 스토어 없음으로 처리된다.
    public static partial class Premium
    {
        // 원스토어 개발자센터 > 앱 > 라이선스 관리 의 라이선스 키 (공개키)
        const string OneStoreLicenseKey = "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQDUU6o1gOFu6UXM2+9egmtiGnoYq8lJR72YAKvAThUaFXajkVrpD9+mVUpujnrOFsAmMAh0xxKxi8Z7xajmnj6m8tEXMuDHi3Yov6He4azwaVTFTvUUYHIvWvW5aDl9ZvGlYeWpnMG/0QUeGw39JPSzePOKkY1zKmoy6BoZLGCCwwIDAQAB";

        // 원스토어 개발자센터에 등록한 가격. 스토어 조회가 늦거나 실패해도 구매 버튼에 가격이 보이게 한다 (조회되면 스토어 값으로 바뀐다).
        const string RegisteredPrice = "₩1,000";

        static PurchaseClientImpl client;
        static readonly OneStoreCallback callback = new OneStoreCallback();

        static void Reconnect()
        {
            client?.EndConnection();
            client = null;
            Connect();
        }

        static void Connect()
        {
            if (client != null) return; // 언어를 바꾸면 장면을 다시 불러온다 — 연결은 한 번만
            if (Price == null) Price = RegisteredPrice;
            try
            {
                client = new PurchaseClientImpl(OneStoreLicenseKey);
                client.Initialize(callback);
                client.QueryProductDetails(new ReadOnlyCollection<string>(new List<string> { ProductId }), ProductType.INAPP);
            }
            catch (Exception e)
            {
                client = null;
                Debug.LogWarning($"[ONEstore] 연결 실패: {e.Message}");
            }
        }

        public static void Buy()
        {
            if (Owned) return;
            if (client == null || !StoreReady) { StoreUnavailable(); return; }
            Purchasing = true;
            Changed?.Invoke();
            client.Purchase(new PurchaseFlowParams.Builder()
                .SetProductId(ProductId)
                .SetProductType(ProductType.INAPP)
                .SetQuantity(1)
                .Build());
        }

        // 원스토어 구매 내역을 다시 받아 확인한다
        public static void Restore()
        {
            if (client == null) { StoreUnavailable(); return; }
            client.QueryPurchases(ProductType.INAPP);
        }

        static void OnPurchases(List<PurchaseData> purchases)
        {
            bool bought = Purchasing; // 구매 창에서 온 결과면 '적용되었습니다' 알림을 띄운다 (복원·시작 시 조회는 조용히)
            Purchasing = false;
            foreach (var p in purchases)
            {
                if (p.ProductId != ProductId || p.PurchaseState != (int)PurchaseState.PURCHASED) continue;
                Grant(bought);
                if (!p.Acknowledged) client.AcknowledgePurchase(p, ProductType.INAPP); // 확인하지 않으면 원스토어가 3일 뒤 자동 환불한다
            }
            Changed?.Invoke();
        }

        static void OnFailed(IapResult r)
        {
            Purchasing = false;
            Changed?.Invoke();
            if (r.Code == (int)ResponseCode.RESULT_USER_CANCELED) return;
            if (r.Code == (int)ResponseCode.RESULT_ITEM_ALREADY_OWNED) { client.QueryPurchases(ProductType.INAPP); return; } // 이미 산 상품 → 복원
            Debug.LogWarning($"[ONEstore] 실패: {r.Code} {r.Message}");
            Game.I.Fail(L.T("구매에 실패했습니다.", "Purchase failed."));
        }

        // 원스토어는 price 를 숫자만("1100") 줄 때가 있다 → 통화 코드로 기호·자릿수를 붙인다. 이미 기호가 붙어 있으면 그대로 쓴다.
        static string FormatPrice(ProductDetail d)
        {
            string raw = d.price?.Trim();
            if (!string.IsNullOrEmpty(raw) && raw.Any(ch => !char.IsDigit(ch) && ch != '.' && ch != ',')) return raw;
            decimal amount;
            if (d.priceAmountMicros > 0) amount = d.priceAmountMicros / 1000000m;
            else if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out amount)) return null;
            string code = string.IsNullOrEmpty(d.priceCurrencyCode) ? "KRW" : d.priceCurrencyCode.ToUpperInvariant();
            bool noDecimals = code == "KRW" || code == "JPY" || code == "TWD" || code == "VND" || code == "IDR";
            string num = amount.ToString(noDecimals ? "#,0" : "#,0.00", CultureInfo.InvariantCulture);
            switch (code)
            {
                case "KRW": return "₩" + num;
                case "USD": return "$" + num;
                case "JPY": return "¥" + num;
                case "EUR": return "€" + num;
                case "GBP": return "£" + num;
                case "TWD": return "NT$" + num;
                default: return num + " " + code;
            }
        }

        class OneStoreCallback : IPurchaseCallback
        {
            public void OnSetupFailed(IapResult r) => Debug.LogWarning($"[ONEstore] 설정 실패: {r.Code} {r.Message}");

            public void OnProductDetailsSucceeded(List<ProductDetail> details)
            {
                StoreReady = true;
                var d = details?.Find(x => x.productId == ProductId);
                if (d != null) Price = FormatPrice(d);
                client.QueryPurchases(ProductType.INAPP); // 재설치·다른 기기: 구매 내역으로 복원
                Changed?.Invoke();
            }

            public void OnProductDetailsFailed(IapResult r) => Debug.LogWarning($"[ONEstore] 상품 조회 실패: {r.Code} {r.Message}");
            public void OnPurchaseSucceeded(List<PurchaseData> purchases) => OnPurchases(purchases);
            public void OnPurchaseFailed(IapResult r) => OnFailed(r);
            public void OnConsumeSucceeded(PurchaseData purchase) { }
            public void OnConsumeFailed(IapResult r) { }
            public void OnAcknowledgeSucceeded(PurchaseData purchase, ProductType type) { }
            public void OnAcknowledgeFailed(IapResult r) => Debug.LogWarning($"[ONEstore] 구매 확인 실패: {r.Code} {r.Message}");
            public void OnManageRecurringProduct(IapResult r, PurchaseData purchase, RecurringAction action) { }

            // 원스토어 서비스가 오래됐거나 없으면 설치·업데이트 화면을 띄운다
            public void OnNeedUpdate() => client.LaunchUpdateOrInstallFlow(r =>
            {
                if (r.Code == (int)ResponseCode.RESULT_OK) Reconnect();
            });

            // 원스토어 로그인이 필요하면 로그인 화면을 띄우고, 성공하면 다시 연결한다
            public void OnNeedLogin() => new OneStoreAuthClientImpl().LaunchSignInFlow(result =>
            {
                Purchasing = false;
                Changed?.Invoke();
                if (result.IsSuccessful()) Reconnect();
            });
        }
    }
}
#endif
