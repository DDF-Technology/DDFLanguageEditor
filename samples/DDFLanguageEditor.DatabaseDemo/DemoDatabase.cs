using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DDFLanguageEditor.Core;

namespace DDFLanguageEditor.DatabaseDemo
{
    public sealed class DemoCustomer
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool IsCustomer { get; set; }

        public DemoCustomer Clone() => new DemoCustomer
        {
            Id = Id,
            FirstName = FirstName,
            LastName = LastName,
            IsCustomer = IsCustomer
        };
    }

    public sealed class DemoDatabaseService : IDdfHostDatabaseService
    {
        private readonly object syncRoot = new object();
        private readonly List<DemoCustomer> customers = new List<DemoCustomer>();
        private int nextId = 1;

        public IReadOnlyList<DemoCustomer> Customers
        {
            get
            {
                lock (syncRoot)
                    return customers.Select(item => item.Clone()).ToList().AsReadOnly();
            }
        }

        public ValueTask<object> ExecuteAsync(DdfHostDatabaseRequest request)
        {
            request.Invocation.CancellationToken.ThrowIfCancellationRequested();
            lock (syncRoot)
            {
                if (request.StatementId == "customer.insert")
                {
                    int id = nextId++;
                    customers.Add(new DemoCustomer
                    {
                        Id = id,
                        FirstName = Convert.ToString(request.Parameters["firstName"]),
                        LastName = Convert.ToString(request.Parameters["lastName"]),
                        IsCustomer = Convert.ToBoolean(request.Parameters["isCustomer"])
                    });
                    return ValueTask.FromResult<object>(id);
                }
                int customerId = Convert.ToInt32(request.Parameters["customerId"]);
                DemoCustomer customer = customers.Single(item => item.Id == customerId);
                if (request.StatementId == "customer.removeLastNameVowels")
                {
                    customer.LastName = new string((customer.LastName ?? string.Empty)
                        .Where(character => !IsVowel(character)).ToArray());
                    return ValueTask.FromResult<object>(customer.Id);
                }
                if (request.StatementId == "customer.countLastNameLetters")
                    return ValueTask.FromResult<object>((customer.LastName ?? string.Empty)
                        .Count(char.IsLetter));
                if (request.StatementId == "customer.byId")
                    return ValueTask.FromResult<object>(ToHostValue(customer));
            }
            throw new InvalidOperationException(
                "Operazione database dimostrativa non riconosciuta.");
        }

        private static bool IsVowel(char character) =>
            "aeiouàèéìòóùAEIOUÀÈÉÌÒÓÙ".IndexOf(character) >= 0;

        private static Dictionary<string, object> ToHostValue(DemoCustomer customer) =>
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["id"] = customer.Id,
                ["firstName"] = customer.FirstName,
                ["lastName"] = customer.LastName,
                ["isCustomer"] = customer.IsCustomer
            };
    }
}
