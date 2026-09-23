using System;
using System.Collections.Generic;

namespace SomethingDownThere
{
    public enum TradeResult { Ready, Empty, Changed, CreditLimit, Unaffordable, Complete }

    // Quotes bind the visible offer to exact session state. Mutations are synchronous,
    // prevalidated and callback-free; UI notifications happen only after the commit.
    public sealed class StationTrade
    {
        public sealed class SaleOffer
        {
            internal readonly StationTrade Owner;
            internal readonly long InventoryRevision, WalletRevision;
            internal bool Used;
            public IReadOnlyList<InventoryItem> Items { get; }
            public long Value { get; }

            internal SaleOffer(StationTrade owner, string id)
            {
                Owner = owner;
                InventoryRevision = owner.inventory.Revision;
                WalletRevision = owner.wallet.Revision;
                var selected = new List<InventoryItem>();
                foreach (var item in owner.inventory.Items)
                    if (item.Sellable && (id == null || item.InstanceId == id)) { selected.Add(item); Value += item.SaleValue; }
                Items = selected.AsReadOnly();
            }
        }

        public sealed class UpgradeOffer
        {
            internal readonly StationTrade Owner;
            internal readonly long WalletRevision;
            internal readonly long EquipmentRevision;
            internal bool Used;
            public EquipmentKind Kind { get; }
            public int OwnedLevel { get; }
            public int LevelCount { get; }
            public int NextLevel => OwnedLevel + 1;
            public int Cost { get; }
            public bool Complete { get; }

            internal UpgradeOffer(StationTrade owner, EquipmentKind kind)
            {
                Owner = owner;
                WalletRevision = owner.wallet.Revision;
                Kind = kind;
                OwnedLevel = owner.Level(kind);
                EquipmentRevision = owner.EquipmentRevision(kind);
                LevelCount = kind == EquipmentKind.Shovel ? owner.shovel.LevelCount : EquipmentProgression.LevelCount;
                Complete = OwnedLevel == LevelCount || (kind == EquipmentKind.Inventory
                    && owner.inventory.Capacity + EquipmentProgression.InventoryIncrease(OwnedLevel) > 256);
                Cost = Complete ? 0 : kind == EquipmentKind.Shovel ? owner.prices[OwnedLevel - 1] : EquipmentProgression.Price(OwnedLevel);
            }
        }

        public sealed class RefillOffer
        {
            internal readonly StationTrade Owner;
            internal readonly long WalletRevision, BatteryRevision;
            internal bool Used;
            public float Amount { get; }
            public float ChargeAfter { get; }
            public decimal Cost { get; }
            public bool Full { get; }
            public bool Partial { get; }

            internal RefillOffer(StationTrade owner)
            {
                Owner = owner;
                WalletRevision = owner.wallet.Revision;
                BatteryRevision = owner.battery.Revision;
                double missing = (double)owner.battery.Capacity - owner.battery.Charge;
                Full = missing <= 0;
                decimal fullCost = missing > (double)SessionWallet.MaximumBalance * EquipmentProgression.FuelPerCredit
                    ? SessionWallet.MaximumBalance + SessionWallet.MinimumUnit
                    : decimal.Ceiling((decimal)missing / (decimal)EquipmentProgression.FuelPerCredit);
                Partial = fullCost > owner.wallet.Balance;
                Cost = Partial ? owner.wallet.Balance : fullCost;
                double affordable = (double)(Cost * (decimal)EquipmentProgression.FuelPerCredit);
                ChargeAfter = Partial ? Math.Min(owner.battery.Capacity, (float)(owner.battery.Charge + affordable)) : owner.battery.Capacity;
                Amount = (float)((double)ChargeAfter - owner.battery.Charge);
                // A tiny payment cannot buy less than the tank can represent.
                if (ChargeAfter <= owner.battery.Charge) { Amount = 0; Cost = 0; }
            }
        }

        private readonly SessionInventory inventory;
        private readonly SessionWallet wallet;
        private readonly ShovelState shovel;
        private readonly Battery battery;
        private readonly int[] prices;
        public static int[] DefaultPrices() => (int[])EquipmentProgression.TierPrices.Clone();

        public StationTrade(SessionInventory inventory, SessionWallet wallet, ShovelState shovel, int[] prices, Battery battery = null)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            this.shovel = shovel ?? throw new ArgumentNullException(nameof(shovel));
            this.battery = battery;
            if (prices == null || prices.Length != shovel.LevelCount - 1)
                throw new ArgumentException("Provide one price for each shovel upgrade.", nameof(prices));
            this.prices = (int[])prices.Clone();
            foreach (int price in this.prices)
                if (price <= 0) throw new ArgumentException("Upgrade prices must be positive.", nameof(prices));
        }

        public SaleOffer OfferSale(string instanceId = null) => new SaleOffer(this, instanceId);
        public UpgradeOffer OfferUpgrade(EquipmentKind kind = EquipmentKind.Shovel)
        {
            if (kind < EquipmentKind.Shovel || kind > EquipmentKind.Fuel) throw new ArgumentOutOfRangeException(nameof(kind));
            if (kind == EquipmentKind.Fuel && battery == null) throw new InvalidOperationException("Fuel upgrades require the session battery.");
            return new UpgradeOffer(this, kind);
        }
        public RefillOffer OfferRefill() => battery == null ? throw new InvalidOperationException("Refills require the session battery.") : new RefillOffer(this);
        private int Level(EquipmentKind kind) => kind == EquipmentKind.Inventory ? inventory.Level : kind == EquipmentKind.Fuel ? battery.Level : shovel.Level;
        private long EquipmentRevision(EquipmentKind kind) => kind == EquipmentKind.Inventory ? inventory.Revision : kind == EquipmentKind.Fuel ? battery.Revision : shovel.Level;

        public TradeResult Check(SaleOffer offer)
        {
            if (offer == null || offer.Owner != this || offer.Used
                || offer.InventoryRevision != inventory.Revision || offer.WalletRevision != wallet.Revision) return TradeResult.Changed;
            if (offer.Items.Count == 0) return TradeResult.Empty;
            return offer.Value > int.MaxValue - wallet.Balance ? TradeResult.CreditLimit : TradeResult.Ready;
        }

        public TradeResult Check(UpgradeOffer offer)
        {
            if (offer == null || offer.Owner != this || offer.Used || offer.WalletRevision != wallet.Revision
                || offer.OwnedLevel != Level(offer.Kind) || offer.EquipmentRevision != EquipmentRevision(offer.Kind)) return TradeResult.Changed;
            if (offer.Complete) return TradeResult.Complete;
            return wallet.Balance < offer.Cost ? TradeResult.Unaffordable : TradeResult.Ready;
        }

        public bool TrySell(SaleOffer offer)
        {
            if (Check(offer) != TradeResult.Ready) return false;
            offer.Used = true;
            foreach (var item in offer.Items) inventory.TryRemove(item.InstanceId, out _);
            wallet.TryCredit((int)offer.Value);
            return true;
        }

        public bool TryUpgrade(UpgradeOffer offer)
        {
            if (Check(offer) != TradeResult.Ready) return false;
            offer.Used = true;
            wallet.TrySpend(offer.Cost);
            if (offer.Kind == EquipmentKind.Inventory) inventory.TryUpgradeTo(offer.NextLevel);
            else if (offer.Kind == EquipmentKind.Fuel) battery.TryUpgradeTo(offer.NextLevel);
            else shovel.TryUpgradeTo(offer.NextLevel);
            return true;
        }

        public TradeResult Check(RefillOffer offer)
        {
            if (offer == null || offer.Owner != this || offer.Used || offer.WalletRevision != wallet.Revision
                || offer.BatteryRevision != battery.Revision) return TradeResult.Changed;
            if (offer.Full) return TradeResult.Complete;
            return offer.Cost == 0 ? TradeResult.Unaffordable : TradeResult.Ready;
        }

        public bool TryRefill(RefillOffer offer)
        {
            if (Check(offer) != TradeResult.Ready || wallet.Balance < offer.Cost
                || !battery.TryFillTo(offer.ChargeAfter)) return false;
            offer.Used = true;
            // No callbacks occur between validation, fuel delivery and payment.
            // A rejected fuel change can never consume credits or report success.
            wallet.TrySpend(offer.Cost);
            return true;
        }
    }
}
