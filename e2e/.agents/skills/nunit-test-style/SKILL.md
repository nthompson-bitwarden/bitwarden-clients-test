---
name: nunit-test-style
description: Write or refactor this repository's NUnit e2e tests with straight-line shared setup, Arrange/Act/Assert sections, parallel execution, and a separate fixture instance per test case.
license: ../../licenses/bitwarden/LICENSE.txt
---

Read [the portability guide](../../PORTABILITY.md) and [e2e instructions](../../../AGENTS.md).

## Fixture organization

- Give every test fixture its own top-level class in a matching `.cs` file. Do not nest fixture classes inside another class or combine multiple fixture classes in one file.
- Group related fixtures in a feature directory with a matching namespace, such as `Web/Tests/Onboarding/` and `e2e.Web.Tests.Onboarding`.
- Put shared setup in a separate abstract base class and file within the feature directory. Keep feature-specific scenario helpers there too.
- A parameterized fixture may declare multiple `[TestFixture(...)]` cases on its own class; it still belongs in one matching file.

## Shared setup

- Put steps shared by every test in a fixture in `[SetUp]`: construct page objects, prepare the fixture's scenario, and perform shared navigation or login.
- Keep `[SetUp]` straight-line. Do not put branching, loops, switches, ternaries, exception handling, assertions, or test-case selection logic in setup methods. Do not inspect test names, properties, or argument positions to decide what setup does.
- Express scenario differences through explicit fixture parameters or separate fixtures. A shared base setup may call a scenario step overridden by each fixture. Keep case-specific preparation in the test's Arrange section when it is not shared by the fixture.
- Do not hide test-case dispatch in a helper called by setup. Reusable infrastructure operations may handle their own resource management and error recovery.
- Use `[TearDown]` for per-test resources; never share mutable page objects or scenario state between test instances.

## Test bodies

Every test uses these exact sections, in order:

```csharp
//Arrange
var dialog = Page.GetByRole("dialog", "Welcome");

//Act
await dialog.GetByRole("button", "Skip").ClickAsync();

//Assert
await Expect(dialog).Not.ToBeVisibleAsync();
```

Arrange defines case-specific inputs and locators. Act performs the behavior under test. Assert verifies the result. Keep assertions and expectations in test classes, never page or component objects. Use locator/navigation waits for synchronization.

## Fixture defaults

The client base test class defines inherited NUnit defaults:

```csharp
[Parallelizable(ParallelScope.All)]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public abstract class BaseTest { }
```

Do not repeat these attributes on each fixture or mark tests nonparallel by default. Give each case its own browser/page and mutable fields. Isolate server mutations as well as browser storage; a separate fixture instance does not isolate a shared backend account.

Instance-per-test-case fixtures cannot use instance `[OneTimeSetUp]` or `[OneTimeTearDown]` methods. Initialize instance fields through per-test shared steps. Use static one-time lifecycle methods or namespace setup fixtures only for resources that truly belong to the whole run.

When reviewing a change, check for one top-level fixture class per file, setup without conditional dispatch, test bodies with the three sections, inherited lifecycle defaults, and shared mutable state that would break parallel execution.
