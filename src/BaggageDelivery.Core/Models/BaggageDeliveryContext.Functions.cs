using Microsoft.EntityFrameworkCore;
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using BaggageDelivery.Core.Models;

namespace BaggageDelivery.Core.Models
{
    public partial class BaggageDeliveryContext
    {

        [DbFunction("UTL_AddBusinessDays", "dbo")]
        public static DateTime? UTL_AddBusinessDays(int? DaysToAdd, DateTime? Date, int? SiteID, string JobEntryType)
        {
            throw new NotSupportedException("This method can only be called from Entity Framework Core queries");
        }

        [DbFunction("UTL_IsBusinessDay", "dbo")]
        public static bool? UTL_IsBusinessDay(DateTime? Date, int? SiteID, string JobEntryType)
        {
            throw new NotSupportedException("This method can only be called from Entity Framework Core queries");
        }

        protected void OnModelCreatingGeneratedFunctions(ModelBuilder modelBuilder)
        {
        }
    }
}
