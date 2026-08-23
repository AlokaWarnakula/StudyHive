/// Mirrors StudyHive.Api's StudentProfileResponse.
class StudentProfile {
  final String id;
  final String userId;
  final String studentNumber;
  final String department;
  final int yearOfStudy;
  final int maxBookingsPerWeek;
  final int penaltyPoints;
  final String? suspendedUntil;
  final bool isActive;

  const StudentProfile({
    required this.id,
    required this.userId,
    required this.studentNumber,
    required this.department,
    required this.yearOfStudy,
    required this.maxBookingsPerWeek,
    required this.penaltyPoints,
    required this.suspendedUntil,
    required this.isActive,
  });

  factory StudentProfile.fromJson(Map<String, dynamic> json) => StudentProfile(
        id: json['id'] as String,
        userId: json['userId'] as String,
        studentNumber: json['studentNumber'] as String,
        department: json['department'] as String,
        yearOfStudy: json['yearOfStudy'] as int,
        maxBookingsPerWeek: json['maxBookingsPerWeek'] as int,
        penaltyPoints: json['penaltyPoints'] as int,
        suspendedUntil: json['suspendedUntil'] as String?,
        isActive: json['isActive'] as bool,
      );
}
