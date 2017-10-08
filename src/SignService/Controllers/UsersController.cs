﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SignService.Models;
using SignService.Services;

namespace SignService.Controllers
{
    [Authorize(Roles = "admin_signservice")]
    [RequireHttps]
    public class UsersController : Controller
    {
        readonly IUserAdminService adminService;
        readonly IKeyVaultAdminService keyVaultAdminService;

        public UsersController(IUserAdminService adminService, IKeyVaultAdminService keyVaultAdminService)
        {
            this.adminService = adminService;
            this.keyVaultAdminService = keyVaultAdminService;
        }
        public async Task<IActionResult> Index()
        {
            var users = await adminService.GetSignServiceUsersAsync();

            return View(users);
        }

        public async Task<IActionResult> Search(string displayName)
        {
            var users = await adminService.GetUsersAsync(displayName);

            return View(users);
        }

        public IActionResult Create()
        {
            return View(new CreateSignServiceUserModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateSignServiceUserModel createModel)
        {
            if (createModel.Configured)
            {
                if (string.IsNullOrWhiteSpace(createModel.KeyVaultUrl)) ModelState.TryAddModelError(nameof(createModel.KeyVaultUrl), $"{nameof(createModel.KeyVaultUrl)} is required when Configured");
                if (string.IsNullOrWhiteSpace(createModel.KeyVaultUrl)) ModelState.TryAddModelError(nameof(createModel.TimestampUrl), $"{nameof(createModel.TimestampUrl)} is required when Configured");
                if (string.IsNullOrWhiteSpace(createModel.KeyVaultUrl)) ModelState.TryAddModelError(nameof(createModel.CertificateName), $"{nameof(createModel.CertificateName)} is required when Configured");
            }
            if (!ModelState.IsValid)
            {
                return View(createModel);
            }

            try
            {
                var res = await adminService.CreateUserAsync(createModel.DisplayName,
                                                             createModel.Username,
                                                             createModel.Configured,
                                                             createModel.KeyVaultUrl,
                                                             createModel.CertificateName,
                                                             createModel.TimestampUrl);

                var user = res.Item1;

                // create the associated key vault if the vault isn't set
                if (string.IsNullOrWhiteSpace(user.KeyVaultUrl))
                {
                    var vault = await keyVaultAdminService.CreateVaultForUserAsync(user.ObjectId.Value.ToString(), user.UserPrincipalName, user.DisplayName);

                    // Update the vault attribute
                    await adminService.UpdateUserAsync(user.ObjectId.Value, user.DisplayName, user.SignServiceConfigured, vault.VaultUri, user.KeyVaultCertificateName, user.TimestampUrl);
                    user.KeyVaultUrl = vault.VaultUri;
                }

                ViewBag.Password = res.Item2;

                return View(nameof(Details), res.Item1);
            }
            catch (Exception e)
            {
                ModelState.AddModelError("", e.Message);
                return View(createModel);
            }
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var user = await adminService.GetUserByObjectIdAsync(id);
            return View(user);
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            var user = await adminService.GetUserByObjectIdAsync(id);

            var model = new UpdateCreateSignServiceUserModel
            {
                ObjectId = user.ObjectId.Value,
                Username = user.UserPrincipalName,
                Configured = user.SignServiceConfigured ?? false,
                CertificateName = user.KeyVaultCertificateName,
                DisplayName = user.DisplayName,
                KeyVaultUrl = user.KeyVaultUrl,
                TimestampUrl = user.TimestampUrl
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, UpdateCreateSignServiceUserModel model)
        {
            if (model.Configured)
            {
                if (string.IsNullOrWhiteSpace(model.KeyVaultUrl)) ModelState.TryAddModelError(nameof(model.KeyVaultUrl), $"{nameof(model.KeyVaultUrl)} is required when Configured");
                if (string.IsNullOrWhiteSpace(model.KeyVaultUrl)) ModelState.TryAddModelError(nameof(model.TimestampUrl), $"{nameof(model.TimestampUrl)} is required when Configured");
                if (string.IsNullOrWhiteSpace(model.KeyVaultUrl)) ModelState.TryAddModelError(nameof(model.CertificateName), $"{nameof(model.CertificateName)} is required when Configured");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                await adminService.UpdateUserAsync(id,
                                                   model.DisplayName?.Trim(),
                                                   model.Configured,
                                                   model.KeyVaultUrl?.Trim(),
                                                   model.CertificateName?.Trim(),
                                                   model.TimestampUrl?.Trim());

                return RedirectToAction(nameof(Details), new { id = id });
            }
            catch (Exception e)
            {
                ModelState.TryAddModelError("", e.Message);
                return View(model);
            }
        }

        public async Task<IActionResult> Delete(Guid id)
        {
            var user = await adminService.GetUserByObjectIdAsync(id);

            var model = new UpdateCreateSignServiceUserModel
            {
                ObjectId = user.ObjectId.Value,
                Username = user.UserPrincipalName,
                Configured = user.SignServiceConfigured ?? false,
                CertificateName = user.KeyVaultCertificateName,
                DisplayName = user.DisplayName,
                KeyVaultUrl = user.KeyVaultUrl,
                TimestampUrl = user.TimestampUrl
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id, UpdateCreateSignServiceUserModel model)
        {
            var user = await adminService.GetUserByObjectIdAsync(id);

            // Soft delete, just clear out the sign service attributes
            try
            {
                await adminService.UpdateUserAsync(id,
                                                   user.DisplayName,
                                                   null,
                                                   null,
                                                   null,
                                                   null);
            }
            catch (Exception)
            {
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> ResetPassword(Guid id)
        {
            var user = await adminService.GetUserByObjectIdAsync(id);

            
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(Guid id, UpdateCreateSignServiceUserModel model)
        {
            try
            {
                var user = await adminService.GetUserByObjectIdAsync(id);
                var pw = await adminService.UpdatePasswordAsync(id);

                ViewBag.Password = pw;

                return View(nameof(Details), user);
            }
            catch (Exception e)
            {
                ModelState.TryAddModelError("", e.Message);
                return View(model);
            }
        }
    }
}