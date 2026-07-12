using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;

namespace KnightsGauntlet.View
{
    // Real store implementation of IIapService on Unity IAP v5 (com.unity.purchasing 5.x).
    // Consumable shilling bundles only. Uses the mandatory two-step purchase flow:
    // PurchaseProduct -> OnPurchasePending (grant + confirm) -> OnPurchaseConfirmed.
    //
    // Granting happens inside OnPurchasePending and is keyed purely by product id, so an
    // interrupted purchase that Unity re-delivers on the next launch still credits the
    // player before we confirm it. Confirmed consumables are not re-fetched afterwards.
    public class UnityIapService : IIapService
    {
        private StoreController _store;
        private bool _ready;

        public bool IsReady => _ready;

        public event Action OnReady;
        public event Action<string> OnPurchased;
        public event Action<string, bool> OnPurchaseFailed;

        public string PriceString(string productId)
        {
            var p = _store?.GetProductById(productId);
            if (p != null && !string.IsNullOrEmpty(p.metadata?.localizedPriceString))
                return p.metadata.localizedPriceString;
            return IapCatalog.FallbackPriceFor(productId); // until the store price loads
        }

        public async void Initialize()
        {
            _store = UnityIAPServices.StoreController();

            // Subscribe to everything BEFORE Connect: a pending purchase from a previous
            // session can fire the moment we connect.
            _store.OnStoreConnected += OnStoreConnected;
            _store.OnStoreDisconnected += f => Debug.LogWarning("[IAP] Store disconnected: " + f.Message);

            _store.OnProductsFetched += OnProductsFetched;
            _store.OnProductsFetchFailed += f => Debug.LogWarning("[IAP] Product fetch failed: " + f.FailureReason);

            _store.OnPurchasePending += OnPurchasePending;
            _store.OnPurchaseConfirmed += OnPurchaseConfirmed;
            _store.OnPurchaseFailed += OnPurchaseFailedInternal;
            _store.OnPurchaseDeferred += d => Debug.Log("[IAP] Purchase deferred (awaiting approval).");

            try { await _store.Connect(); }
            catch (Exception e) { Debug.LogWarning("[IAP] Connect failed: " + e.Message); }
        }

        private void OnStoreConnected()
        {
            var defs = IapCatalog.Shillings
                .Select(b => new ProductDefinition(b.ProductId, ProductType.Consumable))
                .ToList();
            _store.FetchProducts(defs);
        }

        private void OnProductsFetched(List<Product> _)
        {
            _ready = true;
            OnReady?.Invoke();
        }

        public void Buy(string productId)
        {
            var product = _store?.GetProductById(productId);
            if (product == null) { OnPurchaseFailed?.Invoke(productId, false); return; }
            _store.PurchaseProduct(product);
        }

        // Step 2: validate, grant, then confirm. Runs for fresh purchases AND for any
        // purchase re-delivered from an interrupted session.
        private void OnPurchasePending(PendingOrder order)
        {
            string productId = order.CartOrdered.Items().FirstOrDefault()?.Product?.definition.id;
            int amount = IapCatalog.ShillingsFor(productId);

            if (amount > 0 && ValidateReceipt(order))
            {
                SaveData.AddShillings(amount);
                OnPurchased?.Invoke(productId);
            }

            // Always confirm so Unity stops re-delivering it. (An unknown product id or a
            // failed receipt grants nothing but is still finalized rather than looping.)
            _store.ConfirmPurchase(order);
        }

        // Step 3: confirmation result. Content was already granted in the pending step; a
        // FailedOrder here means the platform could not finalize, so surface it.
        private void OnPurchaseConfirmed(Order order)
        {
            if (order is FailedOrder failed)
            {
                string productId = failed.CartOrdered.Items().FirstOrDefault()?.Product?.definition.id;
                Debug.LogWarning("[IAP] Confirmation failed: " + failed.FailureReason + " - " + failed.Details);
                OnPurchaseFailed?.Invoke(productId, false);
            }
        }

        private void OnPurchaseFailedInternal(FailedOrder failed)
        {
            string productId = failed.CartOrdered.Items().FirstOrDefault()?.Product?.definition.id;
            bool cancelled = failed.FailureReason == PurchaseFailureReason.UserCancelled;
            if (!cancelled) Debug.LogWarning("[IAP] Purchase failed: " + failed.FailureReason + " - " + failed.Details);
            OnPurchaseFailed?.Invoke(productId, cancelled);
        }

        // Receipt validation hook. These bundles are consumables granted client-side, which
        // matches the pre-IAP behavior and is fine for launch. To harden against tampering,
        // generate tangle data (Services > In-App Purchasing > Receipt Validation Obfuscator)
        // and validate order.Info.Receipt with a CrossPlatformValidator (Google), and send
        // order.Info.Apple?.jwsRepresentation to a server for Apple StoreKit 2 validation.
        private static bool ValidateReceipt(PendingOrder order) => true;
    }
}
