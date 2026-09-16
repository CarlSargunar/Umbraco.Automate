import { Page } from '@playwright/test';
import { UiHelpers as UmbracoUiHelpers } from '@umbraco-cms/acceptance-test-helpers';
import { ConstantHelper } from './ConstantHelper';
import { AutomateUiHelper } from './AutomateUiHelper';

export class UiHelpers {
  page: Page;
  umbracoUi: UmbracoUiHelpers;
  automate: AutomateUiHelper;

  constructor(page: Page, umbracoUi: UmbracoUiHelpers) {
    this.page = page;
    this.umbracoUi = umbracoUi;
    this.automate = new AutomateUiHelper(page);
  }

  /* Clicks the Automate section tab. Reloads instead when it is already the active section,
   * because clicking an active tab is a no-op and later waits would then hang. */
  async goToAutomateSection() {
    const sectionName = ConstantHelper.sections.automate;
    const sectionLinks = this.page.getByTestId('section-links');
    const header = this.page.locator('umb-backoffice-header');

    await sectionLinks.waitFor({ state: 'visible', timeout: 30000 });

    const alreadySelected = await sectionLinks
      .locator('[active]')
      .getByText(sectionName)
      .isVisible();

    if (alreadySelected) {
      await this.page.reload();
    } else {
      await header.getByRole('tab', { name: sectionName }).click();
    }
  }
}
