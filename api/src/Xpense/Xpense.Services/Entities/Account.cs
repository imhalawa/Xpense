using System;
using System.Collections.Generic;

namespace Xpense.Services.Entities
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

        public virtual required ICollection<Transaction> DepositTransactions { get; set; }
        public virtual required ICollection<Transaction> WithdrawTransactions { get; set; }

        public override bool Equals(object other)
        {
            return AccountNumber == ((Account)other).AccountNumber;
        }
    }
}
