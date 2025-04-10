import { NodeService } from "@/types/rpc/nodeservice";
import * as grpc from "@grpc/grpc-js";

const channel = new grpc.Channel(
  "localhost:42069",
  grpc.credentials.createInsecure(),
  {},
);

export const GetNodeService =
  async (): Promise<NodeService.NodeServiceClient> => {
    return new NodeService.NodeServiceClient(
      "localhost:42069",
      grpc.credentials.createInsecure(),
      { channelOverride: channel },
    );
  };
