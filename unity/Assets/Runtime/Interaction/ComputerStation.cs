using System.Collections.Generic;

namespace SomethingDownThere
{
    public sealed class ComputerStation : StationTarget
    {
        private readonly StationTrade.UpgradeOffer[] offers = new StationTrade.UpgradeOffer[5];
        private StationTrade.SaleOffer[] sales = System.Array.Empty<StationTrade.SaleOffer>();
        public const int RefillCommand = 5;
        public const int LampCommand = 6;
        public const int ChargeCommand = 7;
        public const int SellAllCommand = 8;
        public bool Selling => Items.Count > 0;
        public IReadOnlyList<InventoryItem> Items => sales.Length == 0 ? System.Array.Empty<InventoryItem>() : sales[0].Items;
        public long TotalValue => sales.Length == 0 ? 0 : sales[0].Value;
        public StationTrade.UpgradeOffer OfferAt(int index) => index >= 0 && index < offers.Length ? offers[index] : null;
        public StationTrade.RefillOffer Refill { get; private set; }
        public StationTrade.LampOffer Lamp { get; private set; }
        public StationTrade.ChargeOffer Charge { get; private set; }
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
            Lamp = player.Trade.OfferLamp();
            Charge = player.Trade.OfferCharge();
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
            if (index == LampCommand) return Lamp == null || Lamp.Full ? "Kit full" : $"Buy lamp / ${Lamp.Cost}";
            if (index == ChargeCommand) return Charge == null || Charge.Full ? "Pack full" : $"Buy C4 / ${Charge.Cost}";
            var offer = OfferAt(index);
            return offer == null || offer.Complete ? "Max level" : $"Upgrade / ${offer.Cost}";
        }
        private StationTrade.SaleOffer SaleAt(int index) => index >= SellAllCommand && index < SellAllCommand + sales.Length
            ? sales[index - SellAllCommand] : null;

        public override bool CanExecute(int index, FpsPlayer player) => isActiveAndEnabled && (Selling
            ? player.Trade.Check(SaleAt(index))
            : index == RefillCommand ? player.Trade.Check(Refill)
            : index == LampCommand ? player.Trade.Check(Lamp)
            : index == ChargeCommand ? player.Trade.Check(Charge) : player.Trade.Check(OfferAt(index))) == TradeResult.Ready;

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
            else if (index == LampCommand)
            {
                if (!player.Trade.TryBuyLamp(Lamp)) return false;
                player.ShowStationFeedback($"+1 work lamp  |  -${Lamp.Cost}");
            }
            else if (index == ChargeCommand)
            {
                if (!player.Trade.TryBuyCharge(Charge)) return false;
                player.ShowStationFeedback($"+1 C4 charge  |  -${Charge.Cost}");
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
