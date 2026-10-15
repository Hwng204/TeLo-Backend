namespace Application.Common;

public static class ExamPackageWorkflow
{
    public static bool Allowed(string action, string status, string purpose, bool manager, bool assignedReviewer, bool pht)
    {
        if (purpose != "exam") return false;
        return action switch
        {
            "RENAMED" or "DELETED" => pht && manager && status == "DRAFT",
            "REVIEW_ASSIGNED" => pht && manager && status is "DRAFT" or "IN_REVIEW" or "REVIEWED",
            "QUESTION_REPLACED" or "REVIEW_SUBMITTED" => assignedReviewer && status == "IN_REVIEW",
            "REVIEW_RETURNED" or "FINALIZED" => pht && status == "REVIEWED",
            "PRACTICE_COPIED" => pht && status == "APPROVED",
            _ => false
        };
    }
}
