namespace NT.QAMS.SharedKernel.MultiTenancy;

/// <summary>
/// Organizational allocation of a record inside its tenant: the branch and
/// department the record belongs to. Optional by design — small labs run a
/// single implicit site — and set at creation from the caller's selection
/// (the UI cascades department options from the chosen branch). Reference
/// integrity is by id into the Organization context; names resolve at the
/// read side.
/// </summary>
public interface IAllocatable
{
    Guid? BranchId { get; set; }
    Guid? DepartmentId { get; set; }
}
