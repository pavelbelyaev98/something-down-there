using System;

namespace SomethingDownThere
{
    // Identity comes from the find's owner and survives collection/removal within the session.
    public sealed class InventoryItem
    {
        public string InstanceId { get; }
        public string DisplayName { get; }
        public int SaleValue { get; }
        public DiscoveryKind Kind { get; }
        public bool Sellable => Kind == DiscoveryKind.Common;

        public InventoryItem(string instanceId, string displayName, int saleValue, DiscoveryKind kind = DiscoveryKind.Common)
        {
            if (string.IsNullOrWhiteSpace(instanceId)) throw new ArgumentException("An instance ID is required.", nameof(instanceId));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("A display name is required.", nameof(displayName));
            if (saleValue < 0) throw new ArgumentOutOfRangeException(nameof(saleValue));
            InstanceId = instanceId;
            DisplayName = displayName;
            SaleValue = saleValue;
            if (kind != DiscoveryKind.Common && kind != DiscoveryKind.Unique) throw new ArgumentOutOfRangeException(nameof(kind));
            if (kind == DiscoveryKind.Unique && saleValue != 0) throw new ArgumentException("Uniques cannot have a sale value.");
            Kind = kind;
        }
    }
}
