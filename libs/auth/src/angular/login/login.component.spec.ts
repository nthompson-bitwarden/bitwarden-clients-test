import { ComponentFixture, TestBed } from "@angular/core/testing";
import { FormBuilder } from "@angular/forms";
import { ActivatedRoute, Router, provideRouter } from "@angular/router";
import { mock } from "jest-mock-extended";
import { BehaviorSubject, of } from "rxjs";

import {
  LoginEmailServiceAbstraction,
  LoginStrategyServiceAbstraction,
  LoginSuccessHandlerService,
} from "@bitwarden/auth/common";
import { InternalPolicyService } from "@bitwarden/common/admin-console/abstractions/policy/policy.service.abstraction";
import { DevicesApiServiceAbstraction } from "@bitwarden/common/auth/abstractions/devices-api.service.abstraction";
import { SsoLoginServiceAbstraction } from "@bitwarden/common/auth/abstractions/sso-login.service.abstraction";
import { OrganizationInviteService } from "@bitwarden/common/auth/organization-invite";
import {
  PasswordPreloginData,
  PasswordPreloginService,
} from "@bitwarden/common/auth/password-prelogin";
import { ClientType } from "@bitwarden/common/enums";
import { ErrorResponse } from "@bitwarden/common/models/response/error.response";
import { AppIdService } from "@bitwarden/common/platform/abstractions/app-id.service";
import { BroadcasterService } from "@bitwarden/common/platform/abstractions/broadcaster.service";
import {
  Environment,
  EnvironmentService,
} from "@bitwarden/common/platform/abstractions/environment.service";
import { I18nService } from "@bitwarden/common/platform/abstractions/i18n.service";
import { LogService } from "@bitwarden/common/platform/abstractions/log.service";
import { MessagingService } from "@bitwarden/common/platform/abstractions/messaging.service";
import { PlatformUtilsService } from "@bitwarden/common/platform/abstractions/platform-utils.service";
import { ValidationService } from "@bitwarden/common/platform/abstractions/validation.service";
import { PasswordStrengthServiceAbstraction } from "@bitwarden/common/tools/password-strength";
// This import has been flagged as unallowed for this class. It may be involved in a circular dependency loop.
// eslint-disable-next-line no-restricted-imports
import { AnonLayoutWrapperDataService, ToastService } from "@bitwarden/components";

import { LoginComponentService } from "./login-component.service";
import { LoginComponent, LoginUiState } from "./login.component";

describe("LoginComponent", () => {
  let fixture: ComponentFixture<LoginComponent>;
  let component: LoginComponent;
  const loginStrategyService = mock<LoginStrategyServiceAbstraction>();
  const loginSuccessHandlerService = mock<LoginSuccessHandlerService>();
  let router: Router;
  const i18nService = mock<I18nService>();
  const environmentService = mock<EnvironmentService>();
  const passwordPreloginService = mock<PasswordPreloginService>();
  const platformUtilsService = mock<PlatformUtilsService>();
  const validationService = mock<ValidationService>();

  beforeEach(() => {
    jest.resetAllMocks();
    platformUtilsService.getClientType.mockReturnValue(ClientType.Web);
    i18nService.t.mockImplementation((key, host) => `${key}: ${host}`);
    environmentService.environment$ = new BehaviorSubject(
      mock<Environment>({ getWebVaultUrl: () => "https://vault.example.test" }),
    );
    passwordPreloginService.getPreloginData$.mockReturnValue(
      of(new PasswordPreloginData(mock(), "user@example.test")),
    );

    TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        FormBuilder,
        { provide: LoginStrategyServiceAbstraction, useValue: loginStrategyService },
        { provide: LoginSuccessHandlerService, useValue: loginSuccessHandlerService },
        provideRouter([]),
        { provide: I18nService, useValue: i18nService },
        { provide: EnvironmentService, useValue: environmentService },
        { provide: PasswordPreloginService, useValue: passwordPreloginService },
        { provide: PlatformUtilsService, useValue: platformUtilsService },
        { provide: ValidationService, useValue: validationService },
        ...[
          ActivatedRoute,
          AnonLayoutWrapperDataService,
          AppIdService,
          BroadcasterService,
          DevicesApiServiceAbstraction,
          LoginEmailServiceAbstraction,
          LoginComponentService,
          MessagingService,
          PasswordStrengthServiceAbstraction,
          InternalPolicyService,
          ToastService,
          LogService,
          SsoLoginServiceAbstraction,
          OrganizationInviteService,
        ].map((provide) => ({ provide, useValue: mock() })),
      ],
    });

    router = TestBed.inject(Router);
    jest.spyOn(router, "navigate");
    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
  });

  it("keeps password entry available for retry after invalid credentials", async () => {
    // Arrange
    component.loginUiState = LoginUiState.MASTER_PASSWORD_ENTRY;
    component.formGroup.setValue({
      email: "user@example.test",
      masterPassword: "incorrect-password",
      rememberEmail: true,
    });
    loginStrategyService.logIn.mockRejectedValue(
      new ErrorResponse({ Message: "Username or password is incorrect. Try again." }, 400),
    );

    // Act
    await component.submit();

    // Assert
    expect(loginStrategyService.logIn).toHaveBeenCalledWith(
      expect.objectContaining({ email: "user@example.test", masterPassword: "incorrect-password" }),
    );
    expect(component.formGroup.controls.masterPassword.errors).toEqual({
      error: { message: "invalidMasterPasswordConfirmEmailAndHost: vault.example.test" },
    });
    expect(component.loginUiState).toBe(LoginUiState.MASTER_PASSWORD_ENTRY);
    expect(component.formGroup.controls.masterPassword.enabled).toBe(true);
    expect(component.emailFormControl.value).toBe("user@example.test");
    expect(component.formGroup.controls.rememberEmail.value).toBe(true);
    expect(validationService.showError).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
    expect(loginSuccessHandlerService.run).not.toHaveBeenCalled();

    // Act: editing the password clears the error and permits another submission.
    component.formGroup.controls.masterPassword.setValue("another-password");
    await component.submit();

    // Assert
    expect(loginStrategyService.logIn).toHaveBeenCalledTimes(2);
    expect(loginStrategyService.logIn).toHaveBeenLastCalledWith(
      expect.objectContaining({ email: "user@example.test", masterPassword: "another-password" }),
    );
    expect(router.navigate).not.toHaveBeenCalled();
    expect(loginSuccessHandlerService.run).not.toHaveBeenCalled();
  });
});
