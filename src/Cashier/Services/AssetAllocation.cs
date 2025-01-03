using Cashier.Data;
using Cashier.Lib;
using Cashier.Model;
using System.Text;
using Tomlyn;
using Tomlyn.Model;

namespace Cashier.Services
{
    /// <summary>
    /// All Asset Allocation functionality. Migrated from Cashier.
    /// </summary>
    public class AssetAllocationService
    {
        public List<AssetClass> assetClasses = [];

        private Dictionary<string, AssetClass> _assetClassIndex = [];
        private Dictionary<string, string> _stockIndex = []; // symbol / asset class
        private ISettingsService _settings;
        private IDexieDAL _db;
        private IAccountService _accountService;

        public AssetAllocationService(ISettingsService settings, IDexieDAL dal, IAccountService accountService)
        {
            _settings = settings;
            _db = dal;
            _accountService = accountService;
        }

        /// <summary>
        /// Formats the array of Asset Classes (the end result) for txt output.
        /// The output can be stored for historical purposes, comparison, etc.
        /// </summary>
        /// <returns></returns>
        public string GetTextReport(List<AssetClass> assetClasses)
        {
            var output = new StringBuilder();

            // header
            output.AppendLine("Asset Class       Allocation Current  Diff.  Diff.%  Alloc.Val.  Curr. Val.  Difference");

            foreach (var ac in assetClasses)
            {
                /*
                    {"name": "Asset Class", "width": 22},
                    {"name": "alloc.", "width": 5},
                    {"name": "cur.al.", "width": 6},
                    {"name": "diff.", "width": 6},
                    {"name": "al.val.", "width": 8},
                    {"name": "value", "width": 8},
                    {"name": "loc.cur.", "width": 13},
                    {"name": "diff", "width": 8}
                */

                // Indentation
                var indent = new string(' ', (ac.Depth - 1) * 2);
                var nameWithIndent = indent + ac.Name;

                // name
                output.Append(nameWithIndent.PadRight(20, ' '));
                output.Append("  ");

                // allocation
                var allocation = ac.Allocation.ToString(Constants.NUMBER_FORMAT).PadLeft(6);
                output.Append(allocation);
                output.Append("  ");

                // currentAllocation
                var currentAllocation = ac.CurrentAllocation.ToString(Constants.NUMBER_FORMAT).PadLeft(6);
                output.Append(currentAllocation);
                output.Append("  ");

                // diff
                var diff = ac.Diff.ToString(Constants.NUMBER_FORMAT).PadLeft(5);
                output.Append(diff);
                output.Append("  ");

                var diffPerc = ac.DiffPerc.ToString(Constants.NUMBER_FORMAT).PadLeft(6);
                output.Append(diffPerc);
                output.Append("  ");

                // allocatedValue
                var allocatedValue = ac.AllocatedValue.ToString(Constants.NUMBER_FORMAT).PadLeft(10);
                output.Append(allocatedValue);
                output.Append("  ");

                // currentValue
                var currentValue = ac.CurrentValue.Quantity?.ToString(Constants.NUMBER_FORMAT).PadLeft(10);
                output.Append(currentValue);
                output.Append("  ");

                // diffAmount
                var diffAmount = ac.DiffAmount.ToString(Constants.NUMBER_FORMAT).PadLeft(10);
                output.Append(diffAmount);

                output.AppendLine();
            }
            return output.ToString();
        }

        /// <summary>
        /// Main method
        /// </summary>
        /// <param name="toml">Asset Allocation definition in TOML</param>
        /// <returns></returns>
        public async Task loadFullAssetAllocation(string toml)
        {
            // load definition
            //await LoadAssetAllocation();
            this.assetClasses = ParseDefinition(toml);

            // build asset class index
            _assetClassIndex = BuildAssetClassIndex();

            // build stock index
            BuildStockIndex();

            // load current values
            await LoadCurrentValues();

            // sum group balances
            SumGroupBalances();

            // calculate offsets
            CalculateOffsets();
        }

        private Dictionary<string, AssetClass> BuildAssetClassIndex()
        {
            var index = new Dictionary<string, AssetClass>();
            foreach (var item in Classes)
            {
                index.Add(item.FullName, item);
            }
            return index;
        }

        private void BuildStockIndex()
        {
            foreach (var assetClass in Classes)
            {
                if (assetClass.Symbols.Count == 0) continue;

                foreach (var symbol in assetClass.Symbols)
                {
                    _stockIndex.Add(symbol, assetClass.FullName);
                }
            }
        }

        private void CalculateOffsets()
        {
            var root = _assetClassIndex.First();
            var total = root.Value.CurrentValue.Quantity!.Value;

            foreach (var ac in Classes)
            {
                // calculate current allocation %.
                ac.CurrentAllocation = (total == 0)
                    ? 0
                    : ac.CurrentValue.Quantity!.Value * 100 / total;

                // diff
                ac.Diff = ac.CurrentAllocation - ac.Allocation;

                // diff %
                ac.DiffPerc = (ac.Diff * 100) / ac.Allocation;

                // allocated value
                ac.AllocatedValue = (ac.Allocation * total) / 100;

                // diff amount
                ac.DiffAmount = ac.CurrentValue.Quantity!.Value - ac.AllocatedValue;
            }
        }

        /// <summary>
        /// Loads the Asset Allocation definition by parsing the TOML string.
        /// </summary>
        /// <param name="toml">Definition. Usually read from a file.</param>
        public List<AssetClass> ParseDefinition(string toml)
        {
            // parse TOML
            var model = Toml.ToModel(toml);

            var aa = LinearizeTable(model);
            return aa;
        }

        // Private

        private IEnumerable<KeyValuePair<string, object>> GetSections(TomlTable item)
        {
            var subsections = item.Where(pair => pair.Value is TomlTable)
                .Select(pair => pair);
            return subsections;
        }

        /// <summary>
        /// Load current balances from accounts.
        /// </summary>
        /// <returns></returns>
        private async Task LoadCurrentValues()
        {
            var currency = await _settings.GetDefaultCurrency();
            var data = await _accountService.LoadInvestmentAccounts(_settings, _db);
            var invAccounts = _accountService.ConvertToViewModel(data);

            foreach (var account in invAccounts)
            {
                var amount = Decimal.Zero;
                var commodity = currency;

                // Current Value is populated from Ledger. Only the active accounts will have a value.
                if (!string.IsNullOrEmpty(account.CurrentValue))
                {
                    amount = Decimal.Parse(account.CurrentValue);
                }
                else
                {
                    continue;
                }

                var acctBalance = _accountService.GetAccountBalance(account, currency);
                // the the account balance.
                account.AccountBalance = acctBalance;

                if (account.AccountBalance.Quantity == 0) continue;

                commodity = account.AccountBalance.Currency;

                // Now get the asset class for this commodity.
                var assetClassName = _stockIndex[commodity!];
                if (assetClassName == null)
                {
                    throw new Exception($"Asset class name not found for commodity {commodity}");
                }

                var assetClass = _assetClassIndex[assetClassName];
                assetClass.CurrentValue.Quantity += amount;
                assetClass.CurrentValue.Currency = currency;
            }
        }

        private void RecursivelyDisplay(KeyValuePair<string, object> pair, int level)
        {
            // The indentation is 2 spaces per level.
            var indent = new string(' ', level * 2);

            TomlTable table = (TomlTable)pair.Value;
            Console.WriteLine($"{indent}{pair.Key}");

            // now display the subsections
            var sections = GetSections(table);
            foreach (var section in sections)
            {
                RecursivelyDisplay(section, level + 1);
            }
        }

        /// <summary>
        /// Creates a linear representation of the Asset Allocation table.
        /// It is not a tree but a list of all asset classes.
        /// </summary>
        /// <param name="table"></param>
        /// <param name="nameSpace"></param>
        /// <returns></returns>
        private List<AssetClass> LinearizeTable(TomlTable table, string nameSpace = "")
        {
            var result = new List<AssetClass>();

            var subsections = GetSections(table);
            foreach (var section in subsections)
            {
                var child = new AssetClass();
                if (!string.IsNullOrEmpty(nameSpace))
                {
                    child.FullName = nameSpace + ':' + section.Key;
                }
                else
                {
                    child.FullName = section.Key;
                }

                var childTable = (TomlTable)section.Value;
                child.Allocation = Convert.ToDecimal(childTable["allocation"]);
                if (childTable.ContainsKey("symbols"))
                {
                    var symbolsArray = (TomlArray)childTable["symbols"];
                    child.Symbols = symbolsArray.Select(item => (string)item!).ToList();
                }

                result.Add(child);

                // parse recursively
                var childNamespace = nameSpace;
                if (!string.IsNullOrEmpty(childNamespace))
                {
                    childNamespace += ':';
                }
                childNamespace += section.Key;

                var grandchildren = LinearizeTable(childTable, childNamespace);
                result.AddRange(grandchildren);
            }

            return result;
        }

        private void SumGroupBalances()
        {
            var root = _assetClassIndex["Allocation"];

            var sum = SumChildren(root);

            root.CurrentValue.Quantity = sum;
        }

        private Decimal SumChildren(AssetClass item)
        {
            var children = FindChildren(item);
            if (children.Count == 0)
            {
                return item.CurrentValue?.Quantity ?? 0;
            }

            var sum = 0m;
            foreach (var child in children)
            {
                //var sum = children.Sum(child => child.CurrentValue.Amount);
                child.CurrentValue.Quantity = SumChildren(child);
                sum += child.CurrentValue.Quantity ?? 0;
            }
            return sum;
        }

        private List<AssetClass> FindChildren(AssetClass parent)
        {
            return _assetClassIndex
                .Where(x => x.Value.ParentName == parent.FullName)
                .Select(x => x.Value)
                .ToList();
        }
    }
}
