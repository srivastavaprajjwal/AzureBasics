using Microsoft.Azure.Management.Compute.Fluent;
using Microsoft.Azure.Management.Compute.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using System;

namespace AzureWeek1Assignment3
{
    class Program
    {
        static void Main(string[] args)
        {

            try
            {
                //=================================================================
                // Authenticate

                IAzure azure = Azure.Authenticate("C:\\Windows\\system32\\my.azureauth").WithDefaultSubscription();

                // Print selected subscription
                Console.WriteLine("Selected subscription: " + azure.SubscriptionId);

                StartScript(azure);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Authentication Failed.");
                Console.WriteLine(ex);
            }
                  
        }

        /**
        * Function for crearing and managing -
        *  - Virtual Machine
        *  - Disks
        *  - Network security group.
        */
        public static void StartScript(IAzure azure)
        {
            var region = Region.IndiaCentral;
            var vmName = "praj03-vm";
            var rgName = "praj03-rg";
            var userName = "prajjwal";
            var password = "Prajjwal@123";

            try
            {
                //=============================================================
                // Create a virtual machine

                // Prepare a creatable data disk for VM
                var dataDiskCreatable = azure.Disks.Define("praj03-dsk")
                        .WithRegion(region)
                        .WithExistingResourceGroup(rgName)
                        .WithData()
                        .WithSizeInGB(4);

                // Create a data disk to attach to VM
                var dataDisk = azure.Disks.Define("praj03-disk")
                        .WithRegion(region)
                        .WithNewResourceGroup(rgName)
                        .WithData()
                        .WithSizeInGB(4)
                        .Create();

                Console.WriteLine("Creating a Windows VM");

                var vm = azure.VirtualMachines.Define(vmName)
                        .WithRegion(region)
                        .WithNewResourceGroup(rgName)
                        .WithNewPrimaryNetwork("10.0.0.0/28")
                        .WithPrimaryPrivateIPAddressDynamic()
                        .WithoutPrimaryPublicIPAddress().WithPopularWindowsImage(KnownWindowsVirtualMachineImage.WindowsServer2012R2Datacenter)
                        .WithAdminUsername(userName)
                        .WithAdminPassword(password)
                        .WithNewDataDisk(4)
                        .WithNewDataDisk(dataDiskCreatable)
                        .WithExistingDataDisk(dataDisk)
                        .WithSize(VirtualMachineSizeTypes.Parse("Standard_D2s_v3"))
                        .Create();


                // Print virtual machine details
                Console.WriteLine("Virtual Machine Created! with resource ID: " + vm.Id);
                Console.WriteLine("\n===========================\n\n");


                // Update - Tag the virtual machine
                vm.Update()
                        .WithTag("created via", "fluent")
                        .Apply();

                Console.WriteLine("Tagged VM: " + vm.Id);

                //=============================================================
                // Update - Add data disk
                Console.WriteLine("Adding Data Disk to the VM");
                
                vm.Update()
                        .WithNewDataDisk(10)
                        .Apply();

                Console.WriteLine("Added a data disk to VM" + vm.Name);
                Console.WriteLine("\n===========================\n\n");

                //=============================================================
                // Update - detach data disk
                Console.WriteLine("Detaching Data Disk from the VM");

                vm.Update()
                        .WithoutDataDisk(0)
                        .Apply();

                Console.WriteLine("Detached data disk from VM " + vm.Name);
                Console.WriteLine("\n===========================\n\n");

                //=============================================================
                // Create/Update - NSG 
                CreateNSG(azure);
                

                //=============================================================
                // Delete the virtual machine
                Console.WriteLine("Deleting VM: " + vm.Id);

                azure.VirtualMachines.DeleteById(vm.Id);

                Console.WriteLine("Deleted VM: " + vm.Id);
            } catch (Exception ee) {
                Console.WriteLine("Virtual Machine Creation Failed. Find stack trace below:");
                Console.WriteLine(ee);
            }
            finally
            {
                try
                {
                    Console.WriteLine("Deleting Resource Group: " + rgName);
                    azure.ResourceGroups.DeleteByName(rgName);
                    Console.WriteLine("Deleted Resource Group: " + rgName);
                }
                catch (NullReferenceException)
                {
                    Console.WriteLine("Did not create any resources in Azure. No clean up is necessary");
                }
                catch (Exception g)
                {
                    Console.WriteLine(g);
                }
            }
        }

        /**
         * Function for managing network security groups -
         *  - Create a network security group and a subnet
         *  - Update a network security group.
         */
        public static void CreateNSG(IAzure azure)
        {
            string nsgName = "praj03-nsg";
            string rgName = "praj03-rg";
            string vnetName = "praj03-vnet";

            try
            {
                // Define a virtual network for vm

                Console.WriteLine("Creating a virtual network ...");

                var network = azure.Networks.Define(vnetName)
                        .WithRegion(Region.IndiaCentral)
                        .WithNewResourceGroup(rgName)
                        .WithAddressSpace("172.16.0.0/16")
                        .DefineSubnet("prajSubnet")
                            .WithAddressPrefix("172.16.1.0/24")
                            .Attach()
                        .Create();

                Console.WriteLine("Created Virtual Network! Resource id: " + network.Id);
                Console.WriteLine("\n===========================\n\n");


                //============================================================
                // Create a network security group for the subnet

                Console.WriteLine("Creating a security group - allows SSH and HTTP");
                var nsg = azure.NetworkSecurityGroups.Define(nsgName)
                        .WithRegion(Region.IndiaCentral)
                        .WithNewResourceGroup(rgName)
                        .DefineRule("ALLOW-SSH")
                            .AllowInbound()
                            .FromAnyAddress()
                            .FromAnyPort()
                            .ToAnyAddress()
                            .ToPort(22)
                            .WithProtocol(Microsoft.Azure.Management.Network.Fluent.Models.SecurityRuleProtocol.Tcp)
                            .WithPriority(100)
                            .WithDescription("Allow SSH")
                            .Attach()
                        .DefineRule("ALLOW-HTTP")
                            .AllowInbound()
                            .FromAnyAddress()
                            .FromAnyPort()
                            .ToAnyAddress()
                            .ToPort(80)
                            .WithProtocol(Microsoft.Azure.Management.Network.Fluent.Models.SecurityRuleProtocol.Tcp)
                            .WithPriority(101)
                            .WithDescription("Allow HTTP")
                            .Attach()
                        .Create();

                Console.WriteLine("Created a Network Security Group! Resource id:" + nsg.Id);
                Console.WriteLine("\n===========================\n\n");


                //========================================================
                // Update a network security group

                Console.WriteLine("Updating the front end network security group to allow FTP");

                nsg.Update()
                    .DefineRule("ALLOW-FTP")
                            .AllowInbound()
                            .FromAnyAddress()
                            .FromAnyPort()
                            .ToAnyAddress()
                            .ToPortRange(20, 21)
                            .WithProtocol(Microsoft.Azure.Management.Network.Fluent.Models.SecurityRuleProtocol.Tcp)
                            .WithDescription("Allow FTP")
                            .WithPriority(200)
                            .Attach()
                        .Apply();

                Console.WriteLine("Updated the network security group:"+ nsg.Name);
                Console.WriteLine("\n===========================\n\n");
            }
            catch (Exception e)
            {
                Console.WriteLine("NSG Creation/Update Failed. Find stack trace below:");
                Console.WriteLine(e);
            }
        }

    }
}
