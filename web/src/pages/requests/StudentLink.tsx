import { Link } from "react-router-dom";

/**
 * Who made a booking request: name and student number, linking to that student's profile panel
 * on the Students page (/students?id=…). Used on the requests, approvals and review pages so staff
 * always know whose booking they are looking at.
 */
export function StudentLink({
  studentId,
  name,
  studentNumber,
}: {
  studentId: string;
  name?: string | null;
  studentNumber?: string | null;
}) {
  return (
    <span>
      <Link
        to={`/students?id=${encodeURIComponent(studentId)}`}
        onClick={(e) => e.stopPropagation()}
        title="Open student profile"
      >
        <b>{name || "Student"}</b>
      </Link>
      {studentNumber && <div className="fnote">{studentNumber}</div>}
    </span>
  );
}
