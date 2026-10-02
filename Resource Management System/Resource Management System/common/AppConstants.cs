namespace Resource_Management_System.Common
{
    /// <summary>
    /// Centralized role names — used in [Authorize(Roles = ...)] attributes
    /// and anywhere role comparisons happen. Change values here, not in
    /// individual controllers, if role names ever need to change.
    /// </summary>
    public static class Roles
    {
        public const string Department = "Department";
        public const string Approver = "Approver";
        public const string IT = "IT";
        public const string Admin = "Admin";

        public const string ITOrAdmin = "IT,Admin";
        public const string ApproverOrAdmin = "Approver,Admin";
        public const string DepartmentOrAdmin = "Department,Admin";
        public const string DepartmentOrITOrAdmin = "Department,IT,Admin";
    }


    /// <summary>
    /// Centralized request status values — must match the CHECK constraint
    /// on Requests.Status in the database exactly.
    /// </summary>
    public static class RequestStatus
    {
        // A request is reviewed by IT first, then by the two fixed approver accounts.
        public const string PendingITReview = "PendingITReview";
        public const string PendingITReReview = "PendingITReReview";
        public const string PendingApproverOne = "PendingApproverOne";
        public const string PendingApproverTwo = "PendingApproverTwo";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Issued = "Issued";
    }
}
