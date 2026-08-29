namespace OperatorModdedOperations.NativePatching
{
    internal sealed partial class NativePatchAdapterStateMachine
    {
        internal bool TestOnlyEnterOperationalComposite(
            NativeOperationalCapabilityReceipt capabilityReceipt,
            NativeTargetPolicyManifest targetPolicy,
            NativeConstructionApiManifest constructionPolicy,
            NativeBackendReceipt backendReceipt,
            NativeConstructionPhysicalReceipt physicalReceipt,
            out string error)
        {
            lock (gate)
            {
                error = string.Empty;
                if (!IsExactIssuedCapabilityInputs(
                        capabilityReceipt,
                        targetPolicy,
                        constructionPolicy,
                        backendReceipt,
                        physicalReceipt))
                {
                    error = "test capability is not the exact issued receipt";
                    return false;
                }
                operationalCapabilityReceipt = capabilityReceipt;
                state = NativePatchAdapterState.Operational;
                return true;
            }
        }
    }
}
