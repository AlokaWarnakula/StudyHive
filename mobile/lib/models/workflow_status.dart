/// Mirrors StudyHive.Api's WorkflowStatusResponse — one step per agent in the plan
/// (Planner, then the contract-shaped Scheduling/Resource/Validation stubs until S2-S4 land).
class WorkflowStepLog {
  final int stepNumber;
  final String agentName;
  final String? toolName;
  final String? validationResult;
  final String? errorMessage;
  final String? outputJson;

  const WorkflowStepLog({
    required this.stepNumber,
    required this.agentName,
    required this.toolName,
    required this.validationResult,
    required this.errorMessage,
    required this.outputJson,
  });

  factory WorkflowStepLog.fromJson(Map<String, dynamic> json) =>
      WorkflowStepLog(
        stepNumber: json['stepNumber'] as int,
        agentName: json['agentName'] as String,
        toolName: json['toolName'] as String?,
        validationResult: json['validationResult'] as String?,
        errorMessage: json['errorMessage'] as String?,
        outputJson: json['outputJson'] as String?,
      );
}

class WorkflowStatus {
  final String workflowId;
  final String bookingRequestId;
  final String status;
  final String? errorCode;
  final String? errorMessage;
  final List<WorkflowStepLog> steps;

  const WorkflowStatus({
    required this.workflowId,
    required this.bookingRequestId,
    required this.status,
    required this.errorCode,
    required this.errorMessage,
    required this.steps,
  });

  factory WorkflowStatus.fromJson(Map<String, dynamic> json) => WorkflowStatus(
        workflowId: json['workflowId'] as String,
        bookingRequestId: json['bookingRequestId'] as String,
        status: json['status'] as String,
        errorCode: json['errorCode'] as String?,
        errorMessage: json['errorMessage'] as String?,
        steps: (json['steps'] as List<dynamic>? ?? [])
            .map((e) => WorkflowStepLog.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}
