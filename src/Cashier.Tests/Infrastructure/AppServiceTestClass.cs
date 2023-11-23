﻿using Cashier.Data;
using Cashier.Model;
using Cashier.Services;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cashier.Tests.Infrastructure
{
    internal class AppServiceTestClass : AppService
    {
        public AppServiceTestClass(IDexieDAL dal) : base(dal)
        {
        }

        public List<Account> PublicParseAccounts(string[] lines)
        {
            return ParseAccounts(lines);
            
        }
    }
}
