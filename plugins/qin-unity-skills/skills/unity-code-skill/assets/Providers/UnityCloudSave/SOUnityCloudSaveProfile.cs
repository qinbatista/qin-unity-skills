#if SO_DATA_UNITY_CLOUD_SAVE
using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Storage Profile/Unity Cloud Save")]
public sealed class SOUnityCloudSaveProfile : SODataProfile
{
    protected override ISODataKeyStore CreateCloudStore() => new UnityCloudSaveKeyStore();
}
#endif
