import { api } from "./client";
import type { CatalogModule, CatalogRequirement, UserStory } from "../types";

/** Agrega todas las historias de usuario del proyecto vía endpoints de Catálogo. */
export async function loadProjectUserStories(projectId: string): Promise<UserStory[]> {
  const { data: modules } = await api.get<CatalogModule[]>(`/catalog/projects/${projectId}/modules`);
  const stories: UserStory[] = [];
  for (const mod of modules) {
    const { data: requirements } = await api.get<CatalogRequirement[]>(
      `/catalog/modules/${mod.id}/requirements`);
    for (const req of requirements) {
      const { data: reqStories } = await api.get<UserStory[]>(
        `/catalog/requirements/${req.id}/stories`);
      stories.push(...reqStories);
    }
  }
  return stories.sort((a, b) => a.title.localeCompare(b.title, "es"));
}
