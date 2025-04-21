import { NodeServiceClient } from "@/types/wrap/NodeServiceClient";

let _client: NodeServiceClient | undefined = undefined;

export const GetNodeService = async (): Promise<NodeServiceClient> => {
  if (_client === undefined) {
    _client = new NodeServiceClient();
  }
  return _client;
};
