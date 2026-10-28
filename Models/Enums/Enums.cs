using System.ComponentModel.DataAnnotations;

namespace SkillBridge.Models.Enums;

// IMPORTANT: three different concepts are deliberately kept in separate types:
//   JobDifficulty  -> how complex the WORK is (star category of a job/task)
//   SkillLevel     -> a Hunter's self-reported proficiency in ONE skill
//   Rating (1-5)   -> past PERFORMANCE feedback on completed work (see Rating entity)

/// <summary>Difficulty / star category of a job or task (complexity of the work).</summary>
public enum JobDifficulty
{
    [Display(Name = "1-Star (Basic)")] OneStar = 1,
    [Display(Name = "2-Star (Intermediate)")] TwoStar = 2,
    [Display(Name = "3-Star (Advanced)")] ThreeStar = 3
}

/// <summary>A Hunter's proficiency in a particular skill (self-reported).</summary>
public enum SkillLevel
{
    Beginner = 1,
    Intermediate = 2,
    Advanced = 3,
    Expert = 4
}

public enum JobMode
{
    [Display(Name = "Standard (one selected Hunter)")] Standard = 1,
    [Display(Name = "Challenge (multiple participants, first valid submission wins)")] Challenge = 2
}

public enum PaymentType
{
    [Display(Name = "Fixed price")] FixedPrice = 1,
    [Display(Name = "Negotiable (bids)")] Negotiable = 2
}

public enum JobStatus
{
    Open = 1,                                         // accepting applications / challenge participants
    Closed = 2,                                       // no new applications; employer may still select
    [Display(Name = "Awaiting acceptance")] Assigned = 3, // Hunter selected, waiting for Hunter to accept
    [Display(Name = "In progress")] InProgress = 4,
    [Display(Name = "Under review")] UnderReview = 5,
    Completed = 6,
    Cancelled = 7
}

public enum ApplicationStatus
{
    Submitted = 1,
    [Display(Name = "Under review")] UnderReview = 2,
    Shortlisted = 3,
    Accepted = 4,
    Rejected = 5,
    Withdrawn = 6
}

public enum AssignmentStatus
{
    [Display(Name = "Awaiting Hunter acceptance")] PendingAcceptance = 1,
    [Display(Name = "In progress")] InProgress = 2,
    [Display(Name = "Submitted for review")] Submitted = 3,
    [Display(Name = "Revision requested")] RevisionRequested = 4,
    Completed = 5,
    Declined = 6,
    Cancelled = 7,
    [Display(Name = "Challenge closed (not winner)")] ChallengeLost = 8
}

public enum SubmissionStatus
{
    [Display(Name = "Pending review")] PendingReview = 1,
    Approved = 2,
    [Display(Name = "Revision requested")] RevisionRequested = 3,
    [Display(Name = "Rejected (failed review)")] Rejected = 4,
    [Display(Name = "Closed (challenge already won)")] Closed = 5
}

/// <summary>Status of a SIMULATED payment ledger entry. No real money is transferred.</summary>
public enum PaymentStatus
{
    Pending = 1,                                          // work in progress
    [Display(Name = "Awaiting approval")] AwaitingApproval = 2, // work submitted, waiting for employer approval
    Approved = 3,                                         // work approved, payment due
    Paid = 4,                                             // employer recorded a (simulated) payment
    Failed = 5,
    Cancelled = 6,
    Refunded = 7
}

public enum PaymentMethod
{
    [Display(Name = "bKash (simulated)")] Bkash = 1,
    [Display(Name = "Nagad (simulated)")] Nagad = 2,
    [Display(Name = "Bank transfer (simulated)")] BankTransfer = 3,
    [Display(Name = "Cash (recorded manually)")] Cash = 4
}

public enum NotificationType
{
    General = 0,
    NewApplication = 1,
    ApplicationShortlisted = 2,
    ApplicationAccepted = 3,
    ApplicationRejected = 4,
    HunterSelected = 5,
    AssignmentStarted = 6,
    AssignmentDeclined = 7,
    WorkSubmitted = 8,
    RevisionRequested = 9,
    WorkApproved = 10,
    JobCompleted = 11,
    RatingReceived = 12,
    PaymentUpdated = 13,
    ChallengeJoined = 14,
    ChallengeWon = 15,
    ChallengeClosed = 16,
    AccountStatus = 17,
    ReportUpdate = 18
}

public enum ReportStatus
{
    Open = 1,
    Resolved = 2,
    Dismissed = 3
}
