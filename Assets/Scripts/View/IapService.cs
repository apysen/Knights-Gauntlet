using System;

namespace KnightsGauntlet.View
{
    // Store-agnostic in-app-purchase seam. The game only ever talks to this interface,
    // so the real Unity IAP store (UnityIapService) or the dev stub (StubIapService)
    // can be swapped without touching shop UI or game logic. Mirrors IAdService.
    //
    // Only consumable "shilling bundles" are sold. Granting the shillings for a product
    // lives behind this interface (keyed by product id) so a purchase that is interrupted
    // before it finishes is re-granted on the next launch, never lost.
    public interface IIapService
    {
        void Initialize();

        // True once the store is connected and the product catalog has been fetched.
        bool IsReady { get; }

        // Localized store price for a product ("£0.99"), or the catalog fallback string
        // until the real price is fetched (or when running on the stub).
        string PriceString(string productId);

        // Start a purchase. Feedback comes back through the events below, NOT a per-call
        // callback, because a pending purchase can also surface on a later app launch.
        void Buy(string productId);

        // Products fetched / prices updated. UI should refresh displayed prices.
        event Action OnReady;
        // A purchase completed and its shillings were granted (also fires for a purchase
        // re-delivered from a previous, interrupted session).
        event Action<string> OnPurchased;
        // A purchase did not complete. The bool is true when the user cancelled.
        event Action<string, bool> OnPurchaseFailed;
    }

    // One purchasable shilling bundle.
    public struct IapBundle
    {
        public string ProductId;     // must match the store dashboards (App Store / Play)
        public int Shillings;        // granted on a successful purchase
        public string FallbackPrice; // shown until the store's localized price loads
    }

    // The catalog of consumable bundles. Product ids are identical across Apple and
    // Google, so no per-store id mapping is needed. Keep these in sync with the products
    // created in App Store Connect and the Google Play Console.
    public static class IapCatalog
    {
        public static readonly IapBundle[] Shillings =
        {
            new IapBundle { ProductId = "com.apy.knightsgauntlet.shillings5000",   Shillings = 5000,   FallbackPrice = "$0.99"  },
            new IapBundle { ProductId = "com.apy.knightsgauntlet.shillings25000",  Shillings = 25000,  FallbackPrice = "$4.99"  },
            new IapBundle { ProductId = "com.apy.knightsgauntlet.shillings50000",  Shillings = 50000,  FallbackPrice = "$9.99"  },
            new IapBundle { ProductId = "com.apy.knightsgauntlet.shillings100000", Shillings = 100000, FallbackPrice = "$19.99" },
        };

        // Shillings granted by a product id, or 0 if it isn't one of ours.
        public static int ShillingsFor(string productId)
        {
            foreach (var b in Shillings) if (b.ProductId == productId) return b.Shillings;
            return 0;
        }

        public static string FallbackPriceFor(string productId)
        {
            foreach (var b in Shillings) if (b.ProductId == productId) return b.FallbackPrice;
            return "";
        }
    }

    // Dev/default implementation: no store, no charge. Grants instantly so the shop is
    // fully playable in the editor and in beta builds before store products go live.
    public class StubIapService : IIapService
    {
        public bool IsReady => true;
        public string PriceString(string productId) => IapCatalog.FallbackPriceFor(productId);

        public event Action OnReady;
        public event Action<string> OnPurchased;
        public event Action<string, bool> OnPurchaseFailed;

        public void Initialize() { OnReady?.Invoke(); }

        public void Buy(string productId)
        {
            int amount = IapCatalog.ShillingsFor(productId);
            if (amount <= 0) { OnPurchaseFailed?.Invoke(productId, false); return; }
            SaveData.AddShillings(amount);
            OnPurchased?.Invoke(productId);
        }
    }
}
