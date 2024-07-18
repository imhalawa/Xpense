using Xpense.Services.Abstract.Entities;

namespace Xpense.Services.Entities
{
    /// <summary>Account Entity</summary>
    public class Account : BaseEntity, IEquatable<Account>
    {
        /// <summary>
        ///   <para>
        /// Gets or sets the account friendly name.
        /// </para>
        /// </summary>
        /// <value>The account friendly name.</value>
        public required string Name { get; set; }

        /// <summary>Gets or sets the account number.</summary>
        /// <value>The number.</value>
        public required string AccountNumber { get; set; }

        /// <summary>Gets or sets the account balance.</summary>
        /// <value>The balance.</value>
        public decimal Balance { get; set; }

        /// <summary>Gets or sets a value indicating whether this account is default account for transactions.</summary>
        /// <value>
        ///   <para>
        ///     <c>true</c> if this account is the default one for transactions; otherwise, <c>false</c>.
        /// </para>
        /// </value>
        public bool IsDefaultAccount { get; set; }

        public virtual ICollection<Transaction> Transactions { get; set; }

        public bool Equals(Account? other)
        {
            if (other == null) return false;
            return AccountNumber == other.AccountNumber;
        }

        public override bool Equals(object other)
        {
            return AccountNumber == ((Account)other).AccountNumber;
        }

        public void Deposit(decimal amount)
        {
            Balance += amount;
            Touch();
        }

        public void Withdraw(decimal amount)
        {
            Balance -= amount;
            Touch();
        }
    }
}