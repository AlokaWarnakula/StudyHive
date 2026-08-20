namespace StudyHive.Api.Data.Entities;

public enum UserRole { Student, Librarian, StoreOfficer, Admin }

public enum BookingRequestStatus
{
    Draft, Submitted, Processing, PendingApproval,
    Approved, Rejected, RevisionRequested, Completed, Cancelled, Failed
}

public enum WorkflowStatus { Started, InProgress, PendingApproval, Approved, Rejected, Failed, Completed }

public enum StepValidationResult { Pass, Fail, Warning }

public enum RoomBookingStatus { Confirmed, Cancelled, Completed, NoShow }

public enum StockReservationStatus { Pending, Reserved, Released, Used }

public enum StockTransactionType { StockIn, StockOut, Reserve, Release, Adjust }

public enum EmailNotificationStatus { Queued, Sent, Failed, DeadLettered }

public enum QuotationStatus { Draft, Proposed, Approved, Rejected, Superseded }

public enum QuotationLineItemType { Room, Consumable }

public enum ApprovalDecisionType { Approved, Rejected, RevisionRequested }
