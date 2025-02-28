import { INodeService } from "./INodeService";

export class UiService {
  private value = 0;
  private nodeService: INodeService;

  public constructor(nodeService: INodeService) {
    this.nodeService = nodeService;
  }

  public getValue() {
    return this.value;
  }

  public setValue(value: number) {
    this.value = value;
  }

  public async callExample() {
    await this.nodeService.Hi();
  }
}
