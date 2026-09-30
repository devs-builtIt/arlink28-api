namespace Arlink28.Api.Data.Entities;

/// <summary>What a catalogue entry sells. Everything shares one table; HolidayPackage is the original kind.</summary>
public enum ProductType { HolidayPackage, Flight, HotelReservation, VisaSupport }

public enum PackageStatus { Draft, Published, Archived }

public enum PricingBasis { PerParty, PerPerson }

public enum FeatureSection { Included, PremiumService, Highlight, Vehicle, Perk, Excluded, Note }

public enum AddOnUnit { PerStay, PerNight, PerDay, PerPerson }

public enum MediaRole { Hero, Gallery, Poster }

public enum VideoProvider { YouTube, Vimeo }

public enum StaffRole { SuperAdmin, Operator }

public enum StaffTokenType { Invite, PasswordReset }

public enum EnquiryType { Package, General, Booking, Partnership, Career, Investor }

public enum EnquiryStatus { New, Contacted, Closed }
