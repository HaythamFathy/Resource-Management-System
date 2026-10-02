namespace Resource_Management_System.Common
{
    public static class StatusDisplayHelper
    {
        public static (string Label, string CssClasses) GetBadgeInfo(string status)
        {
            return status switch
            {
                /*
                 * IT review
                 */
                RequestStatus.PendingITReview =>
                    ("قيد مراجعة أمين العهده", "status-badge status-review"),

                /*
                 * Request was rejected by an approver
                 * and returned to the custodian/IT review stage.
                 */
                RequestStatus.PendingITReReview =>
                    ("قيد مراجعة أمين العهده", "status-badge status-review"),

                /*
                 * First approval stage
                 */
                RequestStatus.PendingApproverOne =>
                    ("قيد مراجعة نائب رئيس إدارة تكنولوجيا المعلومات", "status-badge status-approver"),

                /*
                 * Second approval stage
                 */
                RequestStatus.PendingApproverTwo =>
                    ("قيد مراجعة مدير إدارة تكنولوجيا المعلومات", "status-badge status-approver"),

                /*
                 * Fully approved and ready for issuing
                 */
                RequestStatus.Approved =>
                    ("تم التصديق", "status-badge status-approved"),

                /*
                 * Final rejection
                 */
                RequestStatus.Rejected =>
                    ("مرفوض", "status-badge status-rejected"),

                /*
                 * Issued remains a real system status,
                 * but it is NOT part of the redesigned filter.
                 */
                RequestStatus.Issued =>
                    ("تم الصرف", "status-badge status-issued"),

                _ =>
                    (status, "status-badge status-default")
            };
        }

        /// <summary>
        /// Simplified status labels shown ONLY to Department users on the
        /// "My Requests" page per Section 7 (Department Status Privacy - DST-01 to DST-16).
        /// Collapses internal workflow states into privacy-safe department-facing states:
        ///   - PendingITReview / PendingITReReview / PendingApproverOne / PendingApproverTwo -> قيد الانتظار
        ///   - Approved                                                                        -> جاهز للصرف
        ///   - Issued                                                                          -> تم الصرف
        ///   - Rejected / RejectedByIT / Any final rejection                                   -> مرفوض
        /// This strictly ensures internal workflow/approver stages and names are never disclosed to Department users.
        /// </summary>
        public static (string Label, string CssClasses) GetDepartmentBadgeInfo(string status)
        {
            return status switch
            {
                RequestStatus.PendingITReview
                    or RequestStatus.PendingITReReview
                    or RequestStatus.PendingApproverOne
                    or RequestStatus.PendingApproverTwo =>
                    ("قيد الانتظار", "status-badge status-review"),

                RequestStatus.Approved =>
                    ("جاهز للصرف", "status-badge status-approved"),

                RequestStatus.Issued =>
                    ("تم الصرف", "status-badge status-issued"),

                RequestStatus.Rejected
                    or "RejectedByIT" =>
                    ("مرفوض", "status-badge status-rejected"),

                _ =>
                    (status, "status-badge status-default")
            };
        }
    }
}