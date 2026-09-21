using System;

namespace SomethingDownThere
{
    // Whole session money. Transactions own when money moves; opening UI never does.
    public sealed class SessionWallet
    {
        public const decimal MinimumUnit = 1m;
        public const decimal MaximumBalance = int.MaxValue;
        public int Balance { get; private set; }
        public int WholeCredits => Balance;
        public long Revision { get; private set; }

        public SessionWallet(int startingBalance = 0)
        {
            if (startingBalance < 0) throw new ArgumentOutOfRangeException(nameof(startingBalance));
            Balance = startingBalance;
        }

        public bool TryCredit(decimal amount)
        {
            if (amount < 0 || amount > MaximumBalance - Balance || amount != decimal.Truncate(amount)) return false;
            Balance += (int)amount;
            if (amount != 0) Revision++;
            return true;
        }

        public bool TrySpend(decimal amount)
        {
            if (amount < 0 || amount > Balance || amount != decimal.Truncate(amount)) return false;
            Balance -= (int)amount;
            if (amount != 0) Revision++;
            return true;
        }
    }
}
