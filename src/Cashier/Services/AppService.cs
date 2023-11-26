﻿using BlazorDexie.Database;
using BlazorDexie.JsModule;
using Cashier.Components.Components;
using Cashier.Components.Pages;
using Cashier.Data;
using Cashier.Model;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Charts;
using System.Dynamic;

namespace Cashier.Services
{
    /// <summary>
    /// Most of the business logic is currently here.
    /// </summary>
    public class AppService
    {
        public IDexieDAL DAL { get; set; }

        public AppService(IDexieDAL dal)
        {
            DAL = dal;
        }

        public async Task deleteAccounts()
        {
            await DAL.Accounts.Clear();
        }

        public async Task<string?> ImportBalanceSheet(string[] lines)
        {
            if (lines.Length == 0)
            {
                throw new ArgumentException("No accounts sent for import!");
            }

            var accounts = ParseAccounts(lines);

            var saveResult = await DAL.Accounts.BulkPut(accounts);
            return saveResult;
        }

        /// <summary>
        /// Imports the payees into storage.
        /// </summary>
        /// <returns></returns>
        public async Task<string> ImportPayees(string[] payeeNames)
        {
            var payees = payeeNames.Select(payee => new Payee { Name = payee });
            var result = await DAL.Payees.BulkAdd(payees);
            return result;
        }

        protected List<Account> ParseAccounts(string[] lines)
        {
            var accounts = new List<Account>();
            var accountBalances = new List<Money>();

            foreach (var line in lines)
            {
                //Console.WriteLine("Processing {0}", line);

                // Prepare for parsing
                var source = line.Trim();
                if (string.IsNullOrEmpty(source)) continue;

                var parts = source.Split("  ");

                Account? account = null;
                if (parts.Length > 1)
                {
                    // name
                    var namePart = parts[1];
                    account = new Account(namePart);
                }

                // Balance
                var balancePart = parts[0];
                // separate the currency and the quantity
                var balanceParts = balancePart.Split(" ");

                // currency
                string currencyPart = string.Empty;
                if (balanceParts.Length > 1)
                {
                    currencyPart = balanceParts[1];
                }

                var amountPart = balanceParts[0];
                // clean-up the thousand-separators
                amountPart = amountPart.Replace(",", string.Empty);

                var accountBalance = new Money(decimal.Parse(amountPart), currencyPart);
                accountBalances.Add(accountBalance);

                // If we do not have a name, it's an amount of a multicurrency account.
                // Keep the balance until we get the line with the account name.
                if (account == null) continue;

                // Once we have the name, assign the balances dictionary and keep for update.
                account!.Balances = accountBalances.ToArray();

                accounts.Add(account);

                // clean-up.
                accountBalances = [];
            }

            return accounts;
        }

        /// <summary>
        /// Returns the next available Xact Id number.
        /// The insert/put is complicated even though the Id is an autoincrement field.
        /// </summary>
        /// <returns></returns>
        public int GetNextXactId()
        {
            //DAL.Xacts.OrderBy("id").;
            // DAL.Xacts.PrimaryKeys()
            return 0;
        }
    }
}
