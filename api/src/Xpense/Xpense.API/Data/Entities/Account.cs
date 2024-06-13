using System;
using System.Collections.Generic;

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
        public string Name { get; set; }

        /// <summary>Gets or sets the account number.</summary>
        /// <value>The number.</value>
        public string AccountNumber { get; set; }

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

        public virtual ICollection<Transaction> DepositTransactions { get; set; }
        public virtual ICollection<Transaction> WithdrawTransactions { get; set; }

        public override bool Equals(object other)
        {
            return this.AccountNumber == ((Account)other).AccountNumber;
        }
    }
}
