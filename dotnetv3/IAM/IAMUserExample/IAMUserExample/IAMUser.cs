﻿// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier:  Apache-2.0

namespace IAMUserExample
{
    using System;
    using System.Threading.Tasks;
    using Amazon;
    using Amazon.IdentityManagement;
    using Amazon.IdentityManagement.Model;
    using Amazon.S3;

    /// <summary>
    /// This example shows a typical use case for the AWS Identity and Access
    /// Management (IAM) service. It aws created using the AWS SDK for .NET
    /// version 3.x and .NET Core 5.x.
    /// </summary>
    public class IAMUser
    {
        // Represents json code for AWS read-only policy for Amazon Simple
        // Storage Service (Amazon S3).
        private const string S3ReadonlyPolicy = "{" +
            "	\"Statement\" : [{" +
                "	\"Action\" : [\"s3:*\"]," +
                "	\"Effect\" : \"Allow\"," +
                "	\"Resource\" : \"*\"" +
            "}]" +
        "}";

        private const string PolicyName = "S3ReadOnlyAccess";

        // Indicates the region where the S3 bucket is located. Remember to replace
        // the value with the endpoit for your own region.
        private static readonly RegionEndpoint AWSRegion = RegionEndpoint.USWest2;

        private static readonly string UserName = "S3ReadOnlyUser";
        private static readonly string GroupName = "S3ReadonlyGroup";

        public static async Task Main()
        {
            var iamClient = new AmazonIdentityManagementServiceClient();

            // Clear the console screen before displaying any text.
            Console.Clear();

            // Create an Amazon Identity Managements Service Group.
            var createGroupResponse = await CreateNewGroupAsync(iamClient, GroupName);

            // Create a policy and add it to the group.
            var success = await AddGroupPermissionsAsync(iamClient, createGroupResponse.Group);

            // Now create a new user.
            User readOnlyUser;
            var userRequest = new CreateUserRequest
            {
                UserName = UserName,
            };

            readOnlyUser = await CreateNewUserAsync(iamClient, userRequest);

            // Create access and secret keys for the user.
            CreateAccessKeyResponse createKeyResponse = await CreateNewAccessKeyAsync(iamClient, UserName);

            // Add the new user to the group.
            success = await AddNewUserToGroupAsync(iamClient, readOnlyUser.UserName, createGroupResponse.Group.GroupName);

            // Show that the user can access Amazon Simple Storage Service
            // (Amazon S3) by listing the buckets on the account.
            Console.Write("Waiting for user status to be Active.");
            do
            {
                Console.Write(" .");
            }
            while (createKeyResponse.AccessKey.Status != StatusType.Active);

            await ListBucketsAsync(createKeyResponse.AccessKey);

            // Delete the user and group.
            await CleanUpResources(iamClient, UserName, GroupName, createKeyResponse.AccessKey.AccessKeyId);

            Console.WriteLine("Press <Enter> to close the program.");
            Console.ReadLine();
        }

        /// <summary>
        /// Creates a new IAM group.
        /// </summary>
        /// <param name="client">The IAM Client object.</param>
        /// <param name="groupName">The string representing the name for the
        /// new group.</param>
        /// <returns>Returns the response object returned by CreateGroupAsync.</returns>
        public static async Task<CreateGroupResponse> CreateNewGroupAsync(
            AmazonIdentityManagementServiceClient client,
            string groupName)
        {
            var createGroupRequest = new CreateGroupRequest
            {
                GroupName = groupName,
            };

            Console.WriteLine("--------------------------------------------------------------------------------------------------------------");
            Console.WriteLine("Start by creating the group...");
            var response = await client.CreateGroupAsync(createGroupRequest);
            Console.WriteLine($"Successfully created the group: {response.Group.GroupName}");
            Console.WriteLine("--------------------------------------------------------------------------------------------------------------\n");

            return response;
        }

        /// <summary>
        /// This method adds Amazon S3 readonly permissions to the group
        /// created earlier.
        /// </summary>
        /// <param name="client">The IAM client object.</param>
        /// <param name="group">The name of the group to create.</param>
        /// <returns>REturns a boolean value that indicates the success of the
        /// PutGroupPolicyAsync call.</returns>
        public static async Task<bool> AddGroupPermissionsAsync(AmazonIdentityManagementServiceClient client, Group group)
        {
            // Add appropriate permissions so the new user can access S3 on
            // a readonly basis.
            var groupPolicyRequest = new PutGroupPolicyRequest
            {
                GroupName = group.GroupName,
                PolicyName = PolicyName,
                PolicyDocument = S3ReadonlyPolicy,
            };

            Console.WriteLine("--------------------------------------------------------------------------------------------------------------");
            var response = await client.PutGroupPolicyAsync(groupPolicyRequest);
            Console.WriteLine($"Successfully added S3 readonly access policy to {group.GroupName}.");

            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }

        /// <summary>
        /// This method creates a new IAM user.
        /// </summary>
        /// <param name="client">The IAM client object.</param>
        /// <param name="request">The user creation request.</param>
        /// <returns>The object returned by the call to CreateUserAsync.</returns>
        public static async Task<User> CreateNewUserAsync(AmazonIdentityManagementServiceClient client, CreateUserRequest request)
        {
            CreateUserResponse response = null;
            try
            {
                response = await client.CreateUserAsync(request);

                // Show the information about the user from the response.
                Console.WriteLine("\n--------------------------------------------------------------------------------------------------------------");
                Console.WriteLine($"New user: {response.User.UserName} ARN = {response.User.Arn}.");
                Console.WriteLine($"{response.User.UserName} has {response.User.PermissionsBoundary}.");
            }
            catch (EntityAlreadyExistsException ex)
            {
                Console.WriteLine($"{ex.Message}");
            }

            if (response is not null)
            {
                return response.User;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Adds the user represented by the userName parameter to the group
        /// represented by the groupName parameter.
        /// </summary>
        /// <param name="client">The client which will be used to make the
        /// method call to add a user to an IAM group.</param>
        /// <param name="userName">A string representing the name of the IAM
        /// group to which the new user will be added.</param>
        /// <param name="groupName">A string representing the name of the group
        /// to which to add the user.</param>
        public static async Task<bool> AddNewUserToGroupAsync(AmazonIdentityManagementServiceClient client, string userName, string groupName)
        {
            var response = await client.AddUserToGroupAsync(new AddUserToGroupRequest
            {
                GroupName = groupName,
                UserName = userName,
            });

            Console.WriteLine("\n--------------------------------------------------------------------------------------------------------------");
            Console.WriteLine($"The user, {userName} has been added to {groupName}.");
            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }

        /// <summary>
        /// Creates a new access key for the user represented by the userName
        /// parameter.
        /// </summary>
        /// <param name="client">The client object which will call
        /// CreateAccessKeyAsync.</param>
        /// <param name="userName">The name of the user for whom an access key
        /// is created by the call to CreateAccessKeyAsync.</param>
        /// <returns>Returns the response from the call to
        /// CreateAccessKeyAsync.</returns>
        public static async Task<CreateAccessKeyResponse> CreateNewAccessKeyAsync(AmazonIdentityManagementServiceClient client, string userName)
        {
            try
            {
                // Create an access key for the IAM user that can be used by the SDK
                var response = await client.CreateAccessKeyAsync(new CreateAccessKeyRequest
                {
                    // Use the user we created in the CreateUser example
                    UserName = UserName,
                });
                return response;
            }
            catch (LimitExceededException e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

        /// <summary>
        /// Proves that the user has the proper permissions to view the
        /// contents of an Amazon S3 bucket.
        /// </summary>
        /// <param name="accessKey">The AccessKey that will provide permissions
        /// for the new user to call ListBucketsAsync.</param>
        public static async Task ListBucketsAsync(AccessKey accessKey)
        {
            Console.WriteLine("\nPress <Enter> to list the S3 buckets using the new user.\n");
            Console.ReadLine();

            // Creating a client that works with this user.
            var client = new AmazonS3Client(accessKey.AccessKeyId, accessKey.SecretAccessKey);

            // Get the list of buckets accessible by the new user.
            var response = await client.ListBucketsAsync();

            // Loop through the list and print each bucket's name
            // and creation date.
            Console.WriteLine("\n--------------------------------------------------------------------------------------------------------------");
            Console.WriteLine("Listing S3 buckets:\n");
            response.Buckets
                .ForEach(b => Console.WriteLine($"Bucket name: {b.BucketName}, created on: {b.CreationDate}"));
        }

        /// <summary>
        /// Deletes the User, Group, and AccessKey which were created for the purposes of
        /// this example.
        /// </summary>
        /// <param name="client">The IAM client used to delete the other
        /// resources.</param>
        /// <param name="userName">The name of the user that will be deleted.</param>
        /// <param name="groupName">The name of the group that will be deleted.</param>
        /// <param name="accessKeyId">The AccessKeyId that represents the
        /// AccessKey that was created for use with the ListBucketsAsync
        /// method.</param>
        public static async Task CleanUpResources(AmazonIdentityManagementServiceClient client, string userName, string groupName, string accessKeyId)
        {
            // Remove the user from the group.
            var removeUserRequest = new RemoveUserFromGroupRequest()
            {
                UserName = userName,
                GroupName = groupName,
            };

            await client.RemoveUserFromGroupAsync(removeUserRequest);

            // Delete the client access keys before deleting the user.
            var deleteAccessKeyRequest = new DeleteAccessKeyRequest()
            {
                AccessKeyId = accessKeyId,
                UserName = userName,
            };

            await client.DeleteAccessKeyAsync(deleteAccessKeyRequest);

            // Now we can safely delete the user.
            var deleteUserRequest = new DeleteUserRequest()
            {
                UserName = userName,
            };

            await client.DeleteUserAsync(deleteUserRequest);

            // We have to delete the policy attached to the group first.
            var deleteGroupPolicyRequest = new DeleteGroupPolicyRequest()
            {
                GroupName = groupName,
                PolicyName = PolicyName,
            };

            await client.DeleteGroupPolicyAsync(deleteGroupPolicyRequest);

            // Now delete the group.
            var deleteGroupRequest = new DeleteGroupRequest()
            {
                GroupName = groupName,
            };

            await client.DeleteGroupAsync(deleteGroupRequest);

            Console.WriteLine("\n--------------------------------------------------------------------------------------------------------------");
            Console.WriteLine("Deleted the user and group created for this example.");
        }
    }
}
