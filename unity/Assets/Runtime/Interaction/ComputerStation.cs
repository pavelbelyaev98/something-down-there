using System.Collections.Generic;

namespace SomethingDownThere
{
    public sealed class ComputerStation : StationTarget
    {
        private readonly StationTrade.UpgradeOffer[] offers = new StationTrade.UpgradeOffer[3];
        private StationTrade.SaleOffer[] sales = System.Array.Empty<StationTrade.SaleOffer>();
        public const int RefillCommand = 3;
        public const int SellAllCommand = 4;
        public bool Selling => Items.Count > 0;
        public IReadOnlyList<InventoryItem> Items => sales.Length == 0 ? System.Array.Empty<InventoryItem>() : sales[0].Items;
        public long TotalValue => sales.Length == 0 ? 0 : sales[0].Value;
        public StationTrade.UpgradeOffer OfferAt(int index) => index >= 0 && index < offers.Length ? offers[index] : null;
        public StationTrade.RefillOffer Refill { get; private set; }
        public override string Title => "Computer";
        public override string GetPrompt(FpsPlayer player) => "Use";
        public override int CommandCount => Selling ? SellAllCommand + sales.Length : SellAllCommand;
        public override string Description(FpsPlayer player) => $"${player.Wallet.Balance}";
        public override void RefreshOffers(FpsPlayer player)
        {
            sales = new StationTrade.SaleOffer[player.Inventory.Count + 1];
            sales[0] = player.Trade.OfferSale();
            for (int i = 1; i < sales.Length; i++) sales[i] = player.Trade.OfferSale(sales[0].Items[i - 1].InstanceId);
            for (int i = 0; i < offers.Length; i++) offers[i] = player.Trade.OfferUpgrade((EquipmentKind)i);
            Refill = player.Trade.OfferRefill();
        }
        public override string CommandLabel(int index, FpsPlayer player)
        {
            if (index >= SellAllCommand)
            {
                var sale = SaleAt(index);
                return sale == null ? "Unavailable" : index == SellAllCommand
                    ? $"Sell all  /  ${TotalValue}" : $"Sell {sale.Items[0].DisplayName}  /  ${sale.Value}";
            }
            if (index == RefillCommand) return Refill == null || Refill.Full ? "Tank full"
                : Refill.Cost == 0 ? "Need $1" : $"Refill / ${Refill.Cost:0}";
            var offer = OfferAt(index);
            return offer == null || offer.Complete ? "Max level" : $"Upgrade / ${offer.Cost}";
        }
        private StationTrade.SaleOffer SaleAt(int index) => index >= SellAllCommand && index < SellAllCommand + sales.Length
            ? sales[index - SellAllCommand] : null;

        public override bool CanExecute(int index, FpsPlayer player) => isActiveAndEnabled && (Selling
            ? player.Trade.Check(SaleAt(index))
            : index == RefillCommand ? player.Trade.Check(Refill) : player.Trade.Check(OfferAt(index))) == TradeResult.Ready;

        public override bool TryExecute(int index, FpsPlayer player)
        {
            if (!player.CanUseStation(this) || !CanExecute(index, player)) return false;
            if (Selling)
            {
                var sale = SaleAt(index);
                if (!player.Trade.TrySell(sale)) return false;
                player.ShowStationFeedback(sale.Items.Count == 1
                    ? $"Sold {sale.Items[0].DisplayName}  |  +${sale.Value}"
                    : $"Sold {sale.Items.Count} finds  |  +${sale.Value}");
            }
            else if (index == RefillCommand)
            {
                if (!player.Trade.TryRefill(Refill)) return false;
                player.ShowStationFeedback($"+{Refill.Amount:0.#} fuel  |  -${Refill.Cost:0}");
            }
            else
            {
                var offer = OfferAt(index);
                if (!player.Trade.TryUpgrade(offer)) return false;
                player.ShowStationFeedback($"{EquipmentProgression.Name(offer.Kind)} upgraded  |  -${offer.Cost}");
            }
            return true;
        }
    }
}
