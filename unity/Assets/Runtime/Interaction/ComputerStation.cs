using System.Collections.Generic;

namespace SomethingDownThere
{
    public sealed class ComputerStation : StationTarget
    {
        private readonly StationTrade.UpgradeOffer[] offers = new StationTrade.UpgradeOffer[4];
        private StationTrade.SaleOffer sale;
        public const int RefillCommand = 4;
        public const int LampCommand = 5;
        public const int SellAllCommand = 6;
        public bool Selling => Items.Count > 0;
        public IReadOnlyList<InventoryItem> Items => sale == null ? System.Array.Empty<InventoryItem>() : sale.Items;
        public long TotalValue => sale == null ? 0 : sale.Value;
        public StationTrade.UpgradeOffer OfferAt(int index) => index >= 0 && index < offers.Length ? offers[index] : null;
        public StationTrade.RefillOffer Refill { get; private set; }
        public StationTrade.LampOffer Lamp { get; private set; }
        public override string Title => "Computer";
        public override string GetPrompt(FpsPlayer player) => "Use";
        // While finds are carried the only command is Sell all; the upgrades follow once the bag is empty.
        public override int CommandCount => Selling ? SellAllCommand + 1 : SellAllCommand;
        public override string Description(FpsPlayer player) => $"${player.Wallet.Balance}";
        public override void RefreshOffers(FpsPlayer player)
        {
            sale = player.Trade.OfferSale();
            for (int i = 0; i < offers.Length; i++) offers[i] = player.Trade.OfferUpgrade((EquipmentKind)i);
            Refill = player.Trade.OfferRefill();
            Lamp = player.Trade.OfferLamp();
        }
        public override string CommandLabel(int index, FpsPlayer player)
        {
            if (index >= SellAllCommand) return SaleAt(index) == null ? "Unavailable" : $"Sell all  +${TotalValue}";
            if (index == RefillCommand) return Refill == null || Refill.Full ? "Battery full"
                : Refill.Cost == 0 ? "Need $1" : $"Recharge / ${Refill.Cost:0}";
            if (index == LampCommand) return Lamp == null || Lamp.Full ? "Kit full" : $"Buy lamp / ${Lamp.Cost}";
            var offer = OfferAt(index);
            return offer == null || offer.Complete ? "Max level" : $"Upgrade / ${offer.Cost}";
        }
        private StationTrade.SaleOffer SaleAt(int index) => index == SellAllCommand ? sale : null;

        public override bool CanExecute(int index, FpsPlayer player) => isActiveAndEnabled && (Selling
            ? player.Trade.Check(SaleAt(index))
            : index == RefillCommand ? player.Trade.Check(Refill)
            : index == LampCommand ? player.Trade.Check(Lamp) : player.Trade.Check(OfferAt(index))) == TradeResult.Ready;

        public override bool TryExecute(int index, FpsPlayer player)
        {
            if (!player.CanUseStation(this) || !CanExecute(index, player)) return false;
            if (Selling)
            {
                var sold = SaleAt(index);
                if (!player.Trade.TrySell(sold)) return false;
                player.ShowStationFeedback(sold.Items.Count == 1
                    ? $"Sold {sold.Items[0].DisplayName}  |  +${sold.Value}"
                    : $"Sold {sold.Items.Count} finds  |  +${sold.Value}");
            }
            else if (index == RefillCommand)
            {
                if (!player.Trade.TryRefill(Refill)) return false;
                player.ShowStationFeedback($"+{Refill.Amount:0.#} charge  |  -${Refill.Cost:0}");
            }
            else if (index == LampCommand)
            {
                if (!player.Trade.TryBuyLamp(Lamp)) return false;
                player.ShowStationFeedback($"+1 work lamp  |  -${Lamp.Cost}");
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
