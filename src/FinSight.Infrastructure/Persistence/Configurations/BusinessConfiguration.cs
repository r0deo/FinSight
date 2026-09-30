using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinSight.Infrastructure.Persistence.Configurations;

public sealed class BusinessConfiguration : IEntityTypeConfiguration<Business>
{
    public void Configure(EntityTypeBuilder<Business> builder)
    {
        builder.HasKey(business => business.Id);

        builder.Property(business => business.Name)
            .IsRequired()
            .HasMaxLength(Business.MaxNameLength);

        builder.Property(business => business.Location)
            .IsRequired()
            .HasMaxLength(Business.MaxLocationLength);

        builder.Property(business => business.Currency)
            .IsRequired()
            .HasMaxLength(Business.CurrencyLength)
            .IsFixedLength();

        builder.Property(business => business.ContactInformation)
            .IsRequired()
            .HasMaxLength(Business.MaxContactInformationLength);
    }
}
