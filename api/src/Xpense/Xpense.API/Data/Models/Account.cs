using Xpense.API.Enums;
using Xpense.API.Resources;

namespace Xpense.API.Data.Models
{
    /// <summary>Account Entity</summary>
    public class Account : BaseEntity
    {

        /// <summary>
        ///   <para>
        /// Gets or sets the account friendly name.
        /// </para>
        /// </summary>
        /// <value>The account friendly name.</value>
        public string Name { get; set; } = Defaults.AccountDefaultName;

        /// <summary>Gets or sets the account number.</summary>
        /// <value>The number.</value>
        public string Number { get; set; } = Defaults.AccountDefaultNumber;

        /// <summary>
        ///   <para> Gets or sets the account type of <see cref="AccountType" /> type.</para>
        /// </summary>
        /// <value>The account type.</value>
        public AccountType Type { get; set; }

        /// <summary>Gets or sets the account balance.</summary>
        /// <value>The balance.</value>
        public decimal Balance { get; set; }

        /// <summary>
        ///   <para>
        /// Gets or sets the account currency of <see cref="CurrencyType" /> type.</para>
        /// </summary>
        /// <value>The currency.</value>
        public Currency Currency { get; set; } = new Currency();

        /// <summary>Gets or sets a value indicating whether this account is transaction locked.</summary>
        /// <value>
        ///   <c>true</c> if this account is transaction locked; otherwise, <c>false</c>.</value>
        public bool IsTransactionLocked { get; set; }

        /// <summary>Gets or sets a value indicating whether this account is default account for transactions.</summary>
        /// <value>
        ///   <para>
        ///     <c>true</c> if this account is the default one for transactions; otherwise, <c>false</c>.
        /// </para>
        /// </value>
        public bool IsDefaultAccount { get; set; }

        public virtual ICollection<Transaction> AccountTransactions { get; set; } = new List<Transaction>();

    }
}
