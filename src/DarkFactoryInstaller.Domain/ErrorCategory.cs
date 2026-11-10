namespace DarkFactoryInstaller.Domain;

/// <summary>Whether a provisioning error is actionable by the operator or an internal fault (issue #10, AC3).</summary>
public enum ErrorCategory
{
    OperatorFixable,
    Internal,
}
