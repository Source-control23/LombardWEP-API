using LombardAPI_NEW.Models;
using Microsoft.EntityFrameworkCore;

namespace LombardAPI_NEW.DataBase
{
    public class DataBaseContext: DbContext
    {
        public DataBaseContext(DbContextOptions<DataBaseContext> options):base(options) {
        
        }
        public DbSet<DL> DLDetails { get; set; }
        public DbSet<Users> users { get; set; }
    }
}
